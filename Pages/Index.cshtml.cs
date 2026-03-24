using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Eva.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("/Login");
        }
    }
}