using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MHARS.Web.Data;
using MHARS.Web.Models;

namespace MHARS.Web.Controllers;

public class SheltersController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, string? district)
    {
        var query = db.Shelters.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || s.Address.Contains(search));
        if (!string.IsNullOrEmpty(district) && district != "All")
            query = query.Where(s => s.District == district);

        ViewBag.Search = search;
        ViewBag.SelectedDistrict = district ?? "All";
        return View(await query.OrderBy(s => s.District).ThenBy(s => s.Name).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        PopulateDistricts();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("Name,District,Address,Capacity,ContactNumber")] Shelter shelter)
    {
        if (!ModelState.IsValid)
        {
            PopulateDistricts();
            return View(shelter);
        }
        db.Add(shelter);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var shelter = await db.Shelters.FindAsync(id);
        if (shelter == null) return NotFound();
        PopulateDistricts();
        return View(shelter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,District,Address,Capacity,ContactNumber")] Shelter shelter)
    {
        if (id != shelter.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            PopulateDistricts();
            return View(shelter);
        }
        var existing = await db.Shelters.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Name = shelter.Name;
        existing.District = shelter.District;
        existing.Address = shelter.Address;
        existing.Capacity = shelter.Capacity;
        existing.ContactNumber = shelter.ContactNumber;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var shelter = await db.Shelters.FirstOrDefaultAsync(s => s.Id == id);
        if (shelter == null) return NotFound();
        return View(shelter);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var shelter = await db.Shelters.FindAsync(id);
        if (shelter != null)
        {
            db.Shelters.Remove(shelter);
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private void PopulateDistricts()
    {
        ViewBag.DistrictList = new SelectList(Districts.List);
    }
}
