using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly JlmsDbContext _db;
    public CustomersController(JlmsDbContext db) => _db = db;

    // GET /api/customers?search=&mobile=&aadhaar=&code=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerListItemDto>>> Search(
        [FromQuery] string? search, [FromQuery] string? mobile,
        [FromQuery] string? aadhaar, [FromQuery] string? code,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.CustomerName.Contains(search));
        if (!string.IsNullOrWhiteSpace(mobile))
            query = query.Where(c => c.Mobile.Contains(mobile));
        if (!string.IsNullOrWhiteSpace(aadhaar))
            query = query.Where(c => c.AadhaarNumber != null && c.AadhaarNumber.Contains(aadhaar));
        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(c => c.CustomerCode.Contains(code));

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<CustomerListItemDto>();
        foreach (var c in customers)
        {
            var activeLoans = await _db.Loans.CountAsync(l => l.CustomerId == c.CustomerId && l.Status == "Active");
            var outstanding = await _db.Loans
                .Where(l => l.CustomerId == c.CustomerId && (l.Status == "Active" || l.Status == "Disbursed"))
                .SumAsync(l => (decimal?)l.OutstandingPrincipal) ?? 0;

            var hasOverdue = await _db.Loans.AnyAsync(l => l.CustomerId == c.CustomerId
                && l.Status == "Active" && l.MaturityDate != null && l.MaturityDate < DateTime.UtcNow.Date);

            string status = activeLoans == 0 ? "No Active Loan" : (hasOverdue ? "Overdue" : "Active");

            result.Add(new CustomerListItemDto(c.CustomerId, c.CustomerCode, c.CustomerName, c.Mobile,
                activeLoans, outstanding, status));
        }
        return Ok(result);
    }

    // GET /api/customers/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetailDto>> GetById(int id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound();

        var activeLoans = await _db.Loans.CountAsync(l => l.CustomerId == id && l.Status == "Active");
        var closedLoans = await _db.Loans.CountAsync(l => l.CustomerId == id && l.Status == "Closed");
        var outstanding = await _db.Loans
            .Where(l => l.CustomerId == id && (l.Status == "Active" || l.Status == "Disbursed"))
            .SumAsync(l => (decimal?)l.OutstandingPrincipal) ?? 0;

        return Ok(new CustomerDetailDto(c.CustomerId, c.CustomerCode, c.CustomerName, c.Gender, c.DateOfBirth,
            c.Mobile, c.AlternateMobile, c.Address, c.City, c.State, c.Pincode, c.AadhaarNumber, c.PanNumber,
            c.KycVerified, activeLoans, outstanding, closedLoans, c.CreatedAt));
    }

    // POST /api/customers
    // POST /api/customers
    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> Create([FromForm] CustomerCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerName) || string.IsNullOrWhiteSpace(dto.Mobile))
            return BadRequest("CustomerName and Mobile are required.");

        var nextSeq = await _db.Customers.CountAsync() + 1;
        var code = $"CUS-{nextSeq:D6}";

        while (await _db.Customers.AnyAsync(c => c.CustomerCode == code))
        {
            nextSeq++;
            code = $"CUS-{nextSeq:D6}";
        }

        // Create upload folders if they don't exist
        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "CustomerPhotos"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "Aadhaar"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "PAN"));

        // Variables to store saved file paths
        string? photoPath = null;
        string? aadhaarPath = null;
        string? panPath = null;

        // Save Customer Photo
        if (dto.CustomerPhoto != null && dto.CustomerPhoto.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.CustomerPhoto.FileName);

            var filePath = Path.Combine(
                uploadsRoot,
                "CustomerPhotos",
                fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.CustomerPhoto.CopyToAsync(stream);
            }

            photoPath = Path.Combine("CustomerPhotos", fileName).Replace("\\", "/");
        }

        // Save Aadhaar
        if (dto.AadhaarFile != null && dto.AadhaarFile.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.AadhaarFile.FileName);

            var filePath = Path.Combine(
                uploadsRoot,
                "Aadhaar",
                fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.AadhaarFile.CopyToAsync(stream);
            }

            aadhaarPath = Path.Combine("Aadhaar", fileName).Replace("\\", "/");
        }

        // Save PAN
        if (dto.PanFile != null && dto.PanFile.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.PanFile.FileName);

            var filePath = Path.Combine(
                uploadsRoot,
                "PAN",
                fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.PanFile.CopyToAsync(stream);
            }

            panPath = Path.Combine("PAN", fileName).Replace("\\", "/");
        }

        var entity = new Customer
        {
            CustomerCode = code,
            CustomerName = dto.CustomerName,
            Gender = dto.Gender,
            DateOfBirth = dto.DateOfBirth,
            Mobile = dto.Mobile,
            AlternateMobile = dto.AlternateMobile,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Pincode = dto.Pincode,
            AadhaarNumber = dto.AadhaarNumber,
            PanNumber = dto.PanNumber,
            BranchId = dto.BranchId,

            // File paths
            PhotoPath = photoPath,
            AadhaarDocPath = aadhaarPath,
            PanDocPath = panPath,

            KycVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.CustomerId },
            new CustomerDetailDto(
                entity.CustomerId,
                entity.CustomerCode,
                entity.CustomerName,
                entity.Gender,
                entity.DateOfBirth,
                entity.Mobile,
                entity.AlternateMobile,
                entity.Address,
                entity.City,
                entity.State,
                entity.Pincode,
                entity.AadhaarNumber,
                entity.PanNumber,
                entity.KycVerified,
                0,
                0,
                0,
                entity.CreatedAt));
    }


        // PUT /api/customers/5
        [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerUpdateDto dto)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CustomerName = dto.CustomerName;
        entity.Gender = dto.Gender;
        entity.DateOfBirth = dto.DateOfBirth;
        entity.Mobile = dto.Mobile;
        entity.AlternateMobile = dto.AlternateMobile;
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.State = dto.State;
        entity.Pincode = dto.Pincode;
        entity.AadhaarNumber = dto.AadhaarNumber;
        entity.PanNumber = dto.PanNumber;
        entity.KycVerified = dto.KycVerified;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
