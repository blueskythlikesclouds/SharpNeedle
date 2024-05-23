using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNeedle.HedgehogEngine;

namespace SharpNeedle.Bodge
{
	internal class Stage
	{
		public string Name;
		public string Light;
		public string Sky;
		public ArchiveList ArchiveList;
		public List<string> ArchiveNames = new();
	}
}
