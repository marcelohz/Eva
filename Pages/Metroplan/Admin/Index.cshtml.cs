using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eva.Pages.Metroplan.Admin
{
    [Authorize(Roles = "ADMIN")]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}