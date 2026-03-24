using System.Security.Claims;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Eva.Services
{
    public interface ICurrentUserService
    {
        string? GetCurrentEmail();
        string? GetCurrentEmpresaCnpj();
        Task<Usuario?> GetCurrentUserAsync();
    }

    public class CurrentUserService : ICurrentUserService
    {
        private readonly EvaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(EvaDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public string? GetCurrentEmail()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name;
        }

        public string? GetCurrentEmpresaCnpj()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirstValue("EmpresaCnpj");
        }

        public async Task<Usuario?> GetCurrentUserAsync()
        {
            var email = GetCurrentEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}
