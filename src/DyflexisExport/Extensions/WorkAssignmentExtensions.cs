using DyflexisExport.Models;
using Google.Apis.Calendar.v3.Data;

namespace DyflexisExport.Extensions
{
	public static class WorkAssignmentExtensions
	{
		public static Event ToEvent(this WorkAssignment workAssignment)
		{
			return new Event
			{
				Summary = workAssignment.Placement,
				Start = new EventDateTime { DateTime = workAssignment.Start.LocalDateTime },
				End = new EventDateTime { DateTime = workAssignment.End.LocalDateTime },
				Description = workAssignment.Id
			};
		}

		public static string ToLogInfo(this WorkAssignment workAssignment) =>
			$"({workAssignment.Id}) [{workAssignment.Start.DateTime:d}] {workAssignment.Start.DateTime:t} -> {workAssignment.End.DateTime:t} - {workAssignment.Placement}";
	}
}
