using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA,ANALISTA")]
    public class MinhasViagensModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MinhasViagensModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Viagem> Viagens { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // The EvaDbContext global query filter already ensures the logged-in
            // company only sees its own trips!
            Viagens = await _context.Viagens
                .OrderByDescending(v => v.Id)
                .ToListAsync();

            return Page();
        }
    }
}