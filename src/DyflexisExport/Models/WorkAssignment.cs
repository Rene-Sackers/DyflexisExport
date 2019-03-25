using System;

namespace DyflexisExport.Models
{
	public class WorkAssignment
	{
		public string Id { get; set; }

		public DateTimeOffset Start { get; set; }

		public DateTimeOffset End { get; set; }

		public string Placement { get; set; }
	}
}
