using System.IO.Hashing;
using System.Xml.Linq;
using SharpNeedle.Bodge;
using SharpNeedle.HedgehogEngine;
using SharpNeedle.HedgehogEngine.Mirage;

string[] directoryPaths = [
	@"C:\Program Files (x86)\Steam\steamapps\common\Sonic Generations\disk",
	@"C:\Software\xenia\content\53450812"
];

string outDirectoryPath =
	@"C:\Program Files (x86)\Steam\steamapps\common\Sonic Generations\mods\GenerationsRaytracing\disk\bb3\Stage";

Directory.CreateDirectory(outDirectoryPath);
Directory.CreateDirectory(Path.Combine(outDirectoryPath, "Common"));

var stages = new List<Stage>();

foreach (string directoryPath in directoryPaths)
{
	foreach (string filePath in Directory.EnumerateFiles(directoryPath, "#*.arl", SearchOption.AllDirectories))
	{
		string stageName = Path.GetFileNameWithoutExtension(filePath)[1..];
		if (stageName.StartsWith("evt") || stageName.StartsWith("Event"))
			continue;

		var archiveList = ResourceUtility.Open<ArchiveList>(filePath);

		var terrainFile = archiveList.GetFile("Terrain.stg.xml");
		if (terrainFile == null)
		{
			if (stageName.Length > 3 && (stageName[3] == '1' || stageName[3] == '2'))
			{
				var originalArchiveList = ResourceUtility.Open<ArchiveList>(
					Path.Combine(Path.GetDirectoryName(filePath), $"#{stageName[..^1]}0.arl"));

				if (originalArchiveList != null)
				{
					terrainFile = originalArchiveList.GetFile("Terrain.stg.xml");
					if (terrainFile != null)
						Console.WriteLine("Unable to locate Terrain.stg.xml ({0})", stageName);
				}
			}
		}

		if (terrainFile != null)
		{
			var sceneEffectFile = archiveList.GetFile("SceneEffect.prm.xml");
			if (sceneEffectFile == null)
				continue;

			string sceneEffect = Encoding.UTF8.GetString(sceneEffectFile.Open().ReadAllBytes());

			sceneEffect = sceneEffect.ReplaceLineEndings("\n");
			sceneEffect = sceneEffect.Replace("</Category>\n  </BloomStar>", "  <Extra>\n        <Param>\n          <BloomType>1</BloomType>\n        </Param>\n      </Extra>\n    </Category>\n  </BloomStar>");
			
			if (!sceneEffect.Contains("BloomType"))
				Console.WriteLine("Unable to add BloomType ({0})", stageName);

			sceneEffectFile.Open(FileAccess.Write).Write(Encoding.UTF8.GetBytes(sceneEffect));

			var newArchiveList = new ArchiveList();
			newArchiveList.Add(sceneEffectFile);

			var resourceArchiveList = ResourceUtility.Open<ArchiveList>(
				Path.Combine(Path.GetDirectoryName(filePath), "Packed", stageName, $"{stageName}.arl"));

			if (resourceArchiveList?.GetFile("terrain.terrain") == null)
				continue;

			string lightDataName = string.Empty;
			string skyModelName = string.Empty;

			var terrain = Encoding.UTF8.GetString(terrainFile.Open().ReadAllBytes());

			int index = terrain.IndexOf("<DataName>");
			if (index == -1)
				continue;
			
			lightDataName = terrain.Substring(index + 10, terrain.IndexOf('<', index + 10) - index - 10);

			index = terrain.IndexOf("<Model>");
			if (index == -1)
				continue;
			
			skyModelName = terrain.Substring(index + 7, terrain.IndexOf('<', index + 7) - index - 7);

			var lightFile = resourceArchiveList.GetFile($"{lightDataName}.light");
			if (lightFile == null)
			{
				Console.WriteLine("Unable to locate {0}.light ({1})", lightDataName, stageName);
				continue;
			}

			newArchiveList.Add(lightFile);

			var skyModelFile = resourceArchiveList.GetFile($"{skyModelName}.model");
			if (skyModelFile == null)
			{
				Console.WriteLine("Unable to locate {0}.model ({1})", skyModelName, stageName);
				continue;
			}

			newArchiveList.Add(skyModelFile);

			var skyModel = new Model();
			skyModel.Read(skyModelFile);

			var pictureFiles = new HashSet<IFile>();

			foreach (var materialName in skyModel.Groups.SelectMany(x => x).Select(x => x.Material.Name).Distinct())
			{
				var materialFile = resourceArchiveList.GetFile($"{materialName}.material");
				if (materialFile != null)
				{
					newArchiveList.Add(materialFile);

					var material = new Material();
					material.Read(materialFile);

					foreach (var texture in material.Texset.Textures)
					{
						var pictureFile = resourceArchiveList.GetFile($"{texture.PictureName}.dds");
						if (pictureFile != null)
							pictureFiles.Add(pictureFile);
						else
							Console.WriteLine("Unable to locate {0}.dds ({1})", texture.PictureName, stageName);
					}
				}
				else
				{
					Console.WriteLine("Unable to locate {0}.material ({1})", materialName, stageName);
				}
			}

			foreach (var pictureFile in pictureFiles)
				newArchiveList.Add(pictureFile);

			stages.Add(new Stage
				{ Name = stageName, Light = lightDataName, Sky = skyModelName, ArchiveList = newArchiveList });
		}
		else
		{
			var stageFile = archiveList.GetFile("Stage.stg.xml");
			if (stageFile != null && archiveList.GetFile("terrain.terrain") != null)
			{
				var sceneEffectFile = archiveList.GetFile("SceneEffect.prm.xml");
				if (sceneEffectFile == null)
					continue;

				string sceneEffect = Encoding.UTF8.GetString(sceneEffectFile.Open().ReadAllBytes());

				sceneEffect = sceneEffect.ReplaceLineEndings("\n");
				sceneEffect = sceneEffect.Replace("ms_FocusNearFarRange", "ms_DefaultFocusNearFarRange");
				sceneEffect = sceneEffect.Replace("</Category>\n  </BloomStar>", "  <Extra>\n        <Param>\n          <BloomType>2</BloomType>\n        </Param>\n      </Extra>\n    </Category>\n  </BloomStar>");
                sceneEffect = sceneEffect.Replace("s_Intensity", "CFxSceneRenderer::m_skyIntensityScale");

				if (!sceneEffect.Contains("BloomType"))
					Console.WriteLine("Unable to add BloomType ({0})", stageName);

				sceneEffectFile.Open(FileAccess.Write).Write(Encoding.UTF8.GetBytes(sceneEffect));

				var newArchiveList = new ArchiveList();
				newArchiveList.Add(sceneEffectFile);

				var resourceArchiveList = ResourceUtility.Open<ArchiveList>(
					Path.Combine(Path.GetDirectoryName(filePath), $"{stageName}.arl"));

				if (resourceArchiveList == null)
					continue;

				string lightDataName = string.Empty;
				string skyModelName = string.Empty;

				var stage = Encoding.UTF8.GetString(stageFile.Open().ReadAllBytes());

				int index = stage.IndexOf("<DataName>");
				if (index == -1)
					continue;

				lightDataName = stage.Substring(index + 10, stage.IndexOf('<', index + 10) - index - 10);

				index = stage.IndexOf("<Model>");
				if (index == -1)
					continue;

				skyModelName = stage.Substring(index + 7, stage.IndexOf('<', index + 7) - index - 7);

				var lightFile = archiveList.GetFile($"{lightDataName}.light");
				if (lightFile == null)
				{
					Console.WriteLine("Unable to locate {0}.light ({1})", lightDataName, stageName);
					continue;
				}

				newArchiveList.Add(lightFile);

				var skyModelFile = resourceArchiveList.GetFile($"{skyModelName}.model");
				if (skyModelFile == null)
				{
					Console.WriteLine("Unable to locate {0}.model ({1})", skyModelName, stageName);
					continue;
				}

				newArchiveList.Add(skyModelFile);

				var skyModel = new Model();
				skyModel.Read(skyModelFile);

				if (skyModel.Groups.SelectMany(x => x).SelectMany(x => x.Elements)
				    .Where(x => x.Type == VertexType.Normal).Any(x => x.Format != VertexFormat.Float3))
					Console.WriteLine("{0}.model has optimized vertex format ({1})", skyModelName, stageName);

				var pictureFiles = new HashSet<IFile>();

				var archiveNames = new List<string>();
				if (stageName.StartsWith("Act"))
				{
					if (stageName.Contains("Beach"))
					{
						archiveNames.Add(stageName.Contains("ActD")
							? "CmnActD_Terrain_Beach"
							: "CmnActN_Terrain_Beach");
					}
					else if (stageName.Contains("China"))
					{
						archiveNames.Add(stageName.Contains("ActD")
							? "CmnActD_Terrain_China"
							: "CmnActN_Terrain_China");
					}
					else if (stageName.Contains("NY"))
					{
						archiveNames.Add(stageName.Contains("ActD") ? 
							"CmnActD_Terrain_NY" : 
							"CmnActN_Terrain_NY");
					}				
					else if (stageName.Contains("Petra"))
					{
						archiveNames.Add(stageName.Contains("ActD") ? 
							"CmnActD_Terrain_Petra" : 
							"CmnActN_Terrain_Petra");
					}
					else if (stageName.Contains("Africa"))
					{
						archiveNames.Add(stageName.Contains("ActD") ? 
							"CmnActD_Terrain_Africa" : 
							"CmnActN_Terrain_Africa");
					}
					else if (stageName.Contains("Mykonos"))
					{
						archiveNames.Add(stageName.Contains("ActD") ? 
							"CmnActD_Terrain_Mykonos" : 
							"CmnActN_Terrain_Mykonos");
					}
					else if (stageName.Contains("Snow"))
					{
						archiveNames.Add(stageName.Contains("ActD") ? 
							"CmnActD_Terrain_Snow" : 
							"CmnActN_Terrain_Snow");
					}
				}

				else if (stageName == "BossDarkGaiaMoray")
					archiveNames.Add("CmnActN_Terrain_Snow");
				else if (stageName == "BossEggLancer")
					archiveNames.Add("CmnActD_Terrain_Beach");
				else if (stageName == "BossPetra")
					archiveNames.Add("CmnActN_Terrain_Petra");
				else if (stageName == "BossPhoenix")
					archiveNames.Add("CmnActN_Terrain_China");
				
				else if (stageName.StartsWith("Town_"))
				{
					if (stageName.Contains("SouthEastAsia"))
					{
						archiveNames.Add("CmnTown_SouthEastAsia");
					}
					else if (stageName.Contains("China"))
					{
						archiveNames.Add("CmnTown_China");
					}
					else if (stageName.Contains("NYCity"))
					{
						archiveNames.Add("CmnTown_NYCity");
					}				
					else if (stageName.Contains("PetraCapital"))
					{
						archiveNames.Add("CmnTown_PetraCapital");
					}			
					else if (stageName.Contains("PetraLabo"))
					{
						archiveNames.Add("CmnTown_PetraCapital");
					}
					else if (stageName.Contains("Africa"))
					{
						archiveNames.Add("CmnTown_Africa");
					}
					else if (stageName.Contains("Mykonos"))
					{
						archiveNames.Add("CmnTown_Mykonos");
					}
					else if (stageName.Contains("Snow"))
					{
						archiveNames.Add("CmnTown_Snow");
					}
					else if (stageName.Contains("EuropeanCity"))
					{
						archiveNames.Add("CmnTown_EuropeanCity");
					}			
					else if (stageName.Contains("EULabo"))
					{
						archiveNames.Add("CmnTown_EuropeanCity");
					}
				}

				var archives = new List<ArchiveList> { resourceArchiveList };
				foreach (var archiveName in archiveNames)
				{
					var archive = ResourceUtility.Open<ArchiveList>(
						Path.Combine(directoryPaths[1], "00040000", $"{archiveName}.arl"));

					if (archive != null)
						archives.Add(archive);
					else
						Console.WriteLine("Unable to locate {0}.arl", archiveName);
				}

				foreach (var materialName in skyModel.Groups.SelectMany(x => x).Select(x => x.Material.Name).Distinct())
				{
					var materialFile = resourceArchiveList.GetFile($"{materialName}.material");
					if (materialFile != null)
					{
						newArchiveList.Add(materialFile);

						var material = new Material();
						material.Read(materialFile);

						if (!material.ShaderName.Equals("DummySky", StringComparison.OrdinalIgnoreCase) && material.ShaderName != "DrawMaterialColor")
						{
							var texsetFile = resourceArchiveList.GetFile($"{materialName}.texset");
							if (texsetFile != null)
							{
								newArchiveList.Add(texsetFile);

								var texset = new Texset();
								texset.Read(texsetFile);

								foreach (var texture in texset.Textures)
								{
									var textureFile = resourceArchiveList.GetFile($"{texture.Name}.texture");
									if (textureFile != null)
									{
										newArchiveList.Add(textureFile);
										
										texture.Read(textureFile);

										bool found = false;

										foreach (var archive in archives)
										{
											var pictureFile = archive.GetFile($"{texture.PictureName}.dds");
											if (pictureFile != null)
											{
												pictureFiles.Add(pictureFile);
												found = true;
												break;
											}
										}

										if (!found)
										{
											Console.WriteLine("Unable to locate {0}.dds ({1})", texture.PictureName, stageName);
										}
									}
									else
									{
										Console.WriteLine("Unable to locate {0}.texture ({1})", texture.Name, stageName);
									}
								}
							}
							else
							{
								Console.WriteLine("Unable to locate {0}.texset ({1})", materialName, stageName);
							}
						}
					}
					else
					{
						Console.WriteLine("Unable to locate {0}.material ({1})", materialName, stageName);
					}
				}

				foreach (var pictureFile in pictureFiles)
					newArchiveList.Add(pictureFile);

				stages.Add(new Stage
					{ Name = stageName, Light = lightDataName, Sky = skyModelName, ArchiveList = newArchiveList });
			}
		}
	}
}

stages.Sort((x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));

var archiveFiles = new Dictionary<ulong, (IFile ArchiveFile, List<Stage> Stages)>();

foreach (var stage in stages)
{
	foreach (var archiveFile in stage.ArchiveList)
	{
		var xxHash = new XxHash64();
		xxHash.Append(MemoryMarshal.Cast<char, byte>(archiveFile.Name.AsSpan()));

		using var stream = archiveFile.Open();
		xxHash.Append(stream);

		var hash = xxHash.GetCurrentHashAsUInt64();
		if (archiveFiles.TryGetValue(hash, out var distinctArchiveFile))
		{
			distinctArchiveFile.Stages.Add(stage);
		}
		else
		{
			archiveFiles.Add(hash, (archiveFile, [stage]));
		}
	}
}

int archiveIndex = 0;

foreach (var archiveGroup in archiveFiles.GroupBy(x => string.Join(",", x.Value.Stages.Select(x => x.Name).Distinct().Order())))
{
	var archiveList = new ArchiveList();
	string archiveName = archiveGroup.Key.Contains(",") ? $"Common/cmn{archiveIndex++:D3}" : archiveGroup.Key;

	foreach (var archiveFile in archiveGroup)
	{
		archiveList.Add(archiveFile.Value.ArchiveFile);

		foreach (var stage in archiveFile.Value.Stages)
			stage.ArchiveNames.Add(archiveName);
	}

	archiveList.Write(Path.Combine(outDirectoryPath, $"{archiveName}.arl"));
}

using var writer = new StreamWriter(Path.Combine(outDirectoryPath, "header_info.h"));

foreach (var stage in stages)
{
	writer.WriteLine("{{ \"{0}\", \"{1}\", \"{2}\", {{ {3} }} }},", stage.Name, stage.Light, stage.Sky, string.Join(", ", stage.ArchiveNames.Select(x => $"\"Stage/{x}\"").Distinct()));
}