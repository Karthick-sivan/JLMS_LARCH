using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JLMS.Api.Data;
using JLMS.Api.Models;
using JLMS.Api.DTOs;
using JLMS.Api.Services;

namespace JLMS.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly JlmsDbContext _db;
    private readonly FinancialYearNumberingService _numbering;
    public CustomersController(JlmsDbContext db, FinancialYearNumberingService numbering)
    {
        _db = db;
        _numbering = numbering;
    }

    // GET /api/customers?search=&mobile=&aadhaar=&code=&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<CustomerListItemDto>>> Search(
        [FromQuery] string? search, [FromQuery] string? mobile,
        [FromQuery] string? aadhaar, [FromQuery] string? code,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.CustomerName.Contains(search));
        if (!string.IsNullOrWhiteSpace(mobile))
            query = query.Where(c => c.Mobile.Contains(mobile));
        if (!string.IsNullOrWhiteSpace(aadhaar))
            query = query.Where(c => c.AadhaarNumber != null && c.AadhaarNumber.Contains(aadhaar));
        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(c => c.CustomerCode.Contains(code));

        var totalCount = await query.CountAsync();

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

            var loanNumbers = await _db.Loans
                .Where(l => l.CustomerId == c.CustomerId)
                .OrderByDescending(l => l.LoanDate)
                .Select(l => l.LoanNumber)
                .ToListAsync();
            string? loanNumbersStr = loanNumbers.Count > 0 ? string.Join(", ", loanNumbers) : null;

            result.Add(new CustomerListItemDto(c.CustomerId, c.CustomerCode, c.CustomerName, c.AadhaarNumber, c.Mobile,
                activeLoans, outstanding, status, loanNumbersStr));
        }

        return Ok(new PagedResultDto<CustomerListItemDto>(result, totalCount, page, pageSize));
    }

    // GET /api/customers/active
    [HttpGet("active")]
    public async Task<ActionResult<List<CustomerActiveListItemDto>>> GetActiveCustomers()
    {
        var customers = await _db.Customers.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerActiveListItemDto(c.CustomerId, c.CustomerCode, c.CustomerName, c.Mobile))
            .ToListAsync();

        return Ok(customers);
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

        return Ok(new CustomerDetailDto(
            CustomerId: c.CustomerId,
            CustomerCode: c.CustomerCode,
            CustomerName: c.CustomerName,
            GuardianName: c.GuardianName,
            Gender: c.Gender,
            DateOfBirth: c.DateOfBirth,
            Mobile: c.Mobile,
            AlternateMobile: c.AlternateMobile,
            Address: c.Address,
            City: c.City,
            State: c.State,
            Pincode: c.Pincode,
            AadhaarNumber: c.AadhaarNumber,
            PanNumber: c.PanNumber,
            KycVerified: c.KycVerified,
            ActiveLoans: activeLoans,
            TotalOutstanding: outstanding,
            ClosedLoans: closedLoans,
            CreatedAt: c.CreatedAt,
            NomineeName: c.NomineeName,
            NomineeMobile: c.NomineeMobile,
            NomineeAddress: c.NomineeAddress,
            NomineeCity: c.NomineeCity,
            NomineeAadhaarNumber: c.NomineeAadhaarNumber,
            NomineePhotoPath: c.NomineePhotoPath,
            NomineeAadhaarDocPath: c.NomineeAadhaarDocPath,
            PhotoPath: c.PhotoPath,
            AadhaarDocPath: c.AadhaarDocPath,
            PanDocPath: c.PanDocPath));
    }

    // GET /api/customers/5/documents/photo|aadhaar|pan|nomineephoto|nomineeaadhaar
    [HttpGet("{id:int}/documents/{docType}")]
    public async Task<IActionResult> GetDocument(int id, string docType)
    {
        var c = await _db.Customers.AsNoTracking()
            .Where(x => x.CustomerId == id)
            .Select(x => new
            {
                x.PhotoPath,
                x.AadhaarDocPath,
                x.PanDocPath,
                x.NomineePhotoPath,
                x.NomineeAadhaarDocPath
            })
            .FirstOrDefaultAsync();

        if (c == null) return NotFound(new { message = "Customer not found." });

        string? relativePath = docType.ToLowerInvariant() switch
        {
            "photo" => c.PhotoPath,
            "aadhaar" => c.AadhaarDocPath,
            "pan" => c.PanDocPath,
            "nomineephoto" => c.NomineePhotoPath,
            "nomineeaadhaar" => c.NomineeAadhaarDocPath,
            _ => null
        };

        if (string.IsNullOrEmpty(relativePath))
            return NotFound(new { message = "No document uploaded for this type." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, relativePath));

        if (!fullPath.StartsWith(Path.GetFullPath(uploadsRoot), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid document path." });

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "File missing on server." });

        var contentType = GetContentType(fullPath);
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, contentType, Path.GetFileName(fullPath));
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    // POST /api/customers
    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> Create([FromForm] CustomerCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerName) || string.IsNullOrWhiteSpace(dto.Mobile))
            return BadRequest("CustomerName and Mobile are required.");

        var branchId = dto.BranchId ?? 1;
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null) return BadRequest("Branch not found.");

        // Customer code now comes from the active "CustomerCode" Financial Year row
        // (Prefix + running sequence), instead of the old hardcoded BRCUS##### counter.
        string code;
        try
        {
            code = await _numbering.GenerateNextCustomerCodeAsync();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "CustomerPhotos"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "Aadhaar"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "PAN"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "NomineePhotos"));
        Directory.CreateDirectory(Path.Combine(uploadsRoot, "NomineeAadhaar"));

        string? photoPath = null;
        string? aadhaarPath = null;
        string? panPath = null;
        string? nomineePhotoPath = null;
        string? nomineeAadhaarPath = null;

        if (dto.CustomerPhoto != null && dto.CustomerPhoto.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.CustomerPhoto.FileName);
            var filePath = Path.Combine(uploadsRoot, "CustomerPhotos", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.CustomerPhoto.CopyToAsync(stream);
            photoPath = Path.Combine("CustomerPhotos", fileName).Replace("\\", "/");
        }

        if (dto.AadhaarFile != null && dto.AadhaarFile.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.AadhaarFile.FileName);
            var filePath = Path.Combine(uploadsRoot, "Aadhaar", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.AadhaarFile.CopyToAsync(stream);
            aadhaarPath = Path.Combine("Aadhaar", fileName).Replace("\\", "/");
        }

        if (dto.PanFile != null && dto.PanFile.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.PanFile.FileName);
            var filePath = Path.Combine(uploadsRoot, "PAN", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.PanFile.CopyToAsync(stream);
            panPath = Path.Combine("PAN", fileName).Replace("\\", "/");
        }

        // Nominee documents are optional — only saved if the user actually uploaded them.
        if (dto.NomineePhoto != null && dto.NomineePhoto.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.NomineePhoto.FileName);
            var filePath = Path.Combine(uploadsRoot, "NomineePhotos", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.NomineePhoto.CopyToAsync(stream);
            nomineePhotoPath = Path.Combine("NomineePhotos", fileName).Replace("\\", "/");
        }

        if (dto.NomineeAadhaarFile != null && dto.NomineeAadhaarFile.Length > 0)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.NomineeAadhaarFile.FileName);
            var filePath = Path.Combine(uploadsRoot, "NomineeAadhaar", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.NomineeAadhaarFile.CopyToAsync(stream);
            nomineeAadhaarPath = Path.Combine("NomineeAadhaar", fileName).Replace("\\", "/");
        }

        var entity = new Customer
        {
            CustomerCode = code,
            CustomerName = dto.CustomerName,
            GuardianName = string.IsNullOrWhiteSpace(dto.GuardianName) ? null : dto.GuardianName,
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

            PhotoPath = photoPath,
            AadhaarDocPath = aadhaarPath,
            PanDocPath = panPath,

            // Nominee fields are all optional — stored as null/blank when not provided.
            NomineeName = string.IsNullOrWhiteSpace(dto.NomineeName) ? null : dto.NomineeName,
            NomineeMobile = string.IsNullOrWhiteSpace(dto.NomineeMobile) ? null : dto.NomineeMobile,
            NomineeAddress = string.IsNullOrWhiteSpace(dto.NomineeAddress) ? null : dto.NomineeAddress,
            NomineeCity = string.IsNullOrWhiteSpace(dto.NomineeCity) ? null : dto.NomineeCity,
            NomineeAadhaarNumber = string.IsNullOrWhiteSpace(dto.NomineeAadhaarNumber) ? null : dto.NomineeAadhaarNumber,
            NomineePhotoPath = nomineePhotoPath,
            NomineeAadhaarDocPath = nomineeAadhaarPath,

            KycVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.CustomerId },
            new CustomerDetailDto(
                CustomerId: entity.CustomerId,
                CustomerCode: entity.CustomerCode,
                CustomerName: entity.CustomerName,
                GuardianName: entity.GuardianName,
                Gender: entity.Gender,
                DateOfBirth: entity.DateOfBirth,
                Mobile: entity.Mobile,
                AlternateMobile: entity.AlternateMobile,
                Address: entity.Address,
                City: entity.City,
                State: entity.State,
                Pincode: entity.Pincode,
                AadhaarNumber: entity.AadhaarNumber,
                PanNumber: entity.PanNumber,
                KycVerified: entity.KycVerified,
                ActiveLoans: 0,
                TotalOutstanding: 0,
                ClosedLoans: 0,
                CreatedAt: entity.CreatedAt,
                NomineeName: entity.NomineeName,
                NomineeMobile: entity.NomineeMobile,
                NomineeAddress: entity.NomineeAddress,
                NomineeCity: entity.NomineeCity,
                NomineeAadhaarNumber: entity.NomineeAadhaarNumber,
                NomineePhotoPath: entity.NomineePhotoPath,
                NomineeAadhaarDocPath: entity.NomineeAadhaarDocPath,
                PhotoPath: entity.PhotoPath,
                AadhaarDocPath: entity.AadhaarDocPath,
                PanDocPath: entity.PanDocPath));
    }

    // PUT /api/customers/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerUpdateDto dto)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CustomerName = dto.CustomerName;
        entity.GuardianName = string.IsNullOrWhiteSpace(dto.GuardianName) ? null : dto.GuardianName;
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

        // Nominee text fields are optional and editable via this endpoint.
        // Nominee documents are not updated here — add a dedicated [FromForm] endpoint if needed.
        entity.NomineeName = string.IsNullOrWhiteSpace(dto.NomineeName) ? null : dto.NomineeName;
        entity.NomineeMobile = string.IsNullOrWhiteSpace(dto.NomineeMobile) ? null : dto.NomineeMobile;
        entity.NomineeAddress = string.IsNullOrWhiteSpace(dto.NomineeAddress) ? null : dto.NomineeAddress;
        entity.NomineeCity = string.IsNullOrWhiteSpace(dto.NomineeCity) ? null : dto.NomineeCity;
        entity.NomineeAadhaarNumber = string.IsNullOrWhiteSpace(dto.NomineeAadhaarNumber) ? null : dto.NomineeAadhaarNumber;

        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/customers/5/documents/photo|aadhaar|pan|nomineephoto|nomineeaadhaar
[HttpPost("{id:int}/documents/{docType}")]
public async Task<IActionResult> UploadDocument(int id, string docType, IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest(new { message = "No file provided." });

    var entity = await _db.Customers.FindAsync(id);
    if (entity == null) return NotFound(new { message = "Customer not found." });

    string folder;
    string? existingPath;
    Action<string> setPath;

    switch (docType.ToLowerInvariant())
    {
        case "photo":
            folder = "CustomerPhotos"; existingPath = entity.PhotoPath;
            setPath = p => entity.PhotoPath = p; break;
        case "aadhaar":
            folder = "Aadhaar"; existingPath = entity.AadhaarDocPath;
            setPath = p => entity.AadhaarDocPath = p; break;
        case "pan":
            folder = "PAN"; existingPath = entity.PanDocPath;
            setPath = p => entity.PanDocPath = p; break;
        case "nomineephoto":
            folder = "NomineePhotos"; existingPath = entity.NomineePhotoPath;
            setPath = p => entity.NomineePhotoPath = p; break;
        case "nomineeaadhaar":
            folder = "NomineeAadhaar"; existingPath = entity.NomineeAadhaarDocPath;
            setPath = p => entity.NomineeAadhaarDocPath = p; break;
        default:
            return BadRequest(new { message = "Invalid document type." });
    }

    // Server-side enforcement: never allow overwriting a doc that's already there.
    if (!string.IsNullOrEmpty(existingPath))
        return BadRequest(new { message = "This document is already uploaded and locked." });

    var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
    Directory.CreateDirectory(Path.Combine(uploadsRoot, folder));

    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
    var filePath = Path.Combine(uploadsRoot, folder, fileName);
    using (var stream = new FileStream(filePath, FileMode.Create))
        await file.CopyToAsync(stream);

    var relativePath = Path.Combine(folder, fileName).Replace("\\", "/");
    setPath(relativePath);
    entity.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    return Ok(new { path = relativePath });
}
}
