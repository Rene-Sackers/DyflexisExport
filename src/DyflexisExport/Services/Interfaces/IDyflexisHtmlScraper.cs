using System.Threading.Tasks;

namespace DyflexisExport.Services.Interfaces
{
	public interface IDyflexisHtmlScraper
	{
		Task<string> GetMonthHtml(int year, int month);
	}
}