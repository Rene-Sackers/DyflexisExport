using System;

namespace DyflexisExport.Models
{
	public class WorkAssignment
	{
		public string Id { get; set; }

		public DateTime Start { get; set; }

		public DateTime End { get; set; }

		public string Placement { get; set; }
	}
}
