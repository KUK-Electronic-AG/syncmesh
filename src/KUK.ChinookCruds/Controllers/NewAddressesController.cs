using AutoMapper;
using KUK.ChinookCruds.ViewModels;
using KUK.Common.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KUK.ChinookCruds.Controllers
{
    public class NewAddressesController : Controller
    {
        private readonly Chinook2Context _context;
        private readonly IMapper _mapper;

        public NewAddressesController(
            Chinook2Context context,
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var addresses = await _context.Addresses
                    .OrderByDescending(a => _context.AddressMappings
                         .Where(am => am.NewAddressId == a.AddressId)
                         .Max(am => (DateTime?)am.MappingTimestamp) ?? DateTime.MaxValue)
                    .ToListAsync();
                return View("/Views/NewDatabaseViews/NewAddresses.cshtml", addresses);
            }
            catch (MySqlException)
            {
                return Content("Unable to connect to the database. Please try again later.");
            }
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var address = await _context.Addresses
                .Include(a => a.Customers)
                .Include(a => a.Invoices)
                .FirstOrDefaultAsync(m => m.AddressId == id);

            if (address == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<NewAddressDetailsViewModel>(address);

            return View("/Views/NewDatabaseViews/DetailsNewAddress.cshtml", viewModel);
        }

        public IActionResult Create()
        {
            return View("/Views/NewDatabaseViews/CreateNewAddress.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Street,City,State,Country,PostalCode")] NewAddressCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var address = _mapper.Map<KUK.Common.ModelsNewSchema.Address>(viewModel);

                try
                {
                    _context.Add(address);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", "Unable to save changes. " + ex.Message);
                }
            }
            return View("/Views/NewDatabaseViews/CreateNewAddress.cshtml", viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var address = await _context.Addresses.FindAsync(id);
            if (address == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<NewAddressEditViewModel>(address);
            return View("/Views/NewDatabaseViews/EditNewAddress.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("AddressId,Street,City,State,Country,PostalCode")] NewAddressEditViewModel viewModel)
        {
            if (id != viewModel.AddressId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var address = _mapper.Map<KUK.Common.ModelsNewSchema.Address>(viewModel);
                    _context.Update(address);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AddressExists(viewModel.AddressId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View("/Views/NewDatabaseViews/EditNewAddress.cshtml", viewModel);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var address = await _context.Addresses
                .FirstOrDefaultAsync(m => m.AddressId == id);
            if (address == null)
            {
                return NotFound();
            }

            return View("/Views/NewDatabaseViews/DeleteNewAddress.cshtml", address);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var address = await _context.Addresses.FindAsync(id);
            if (address == null)
            {
                return NotFound();
            }

            // Check if address is associated with the client
            var isAddressUsedByCustomer = await _context.Customers.AnyAsync(c => c.AddressId == id);
            if (isAddressUsedByCustomer)
            {
                ModelState.AddModelError(string.Empty, "Cannot delete this address because it is associated with one or more customers.");
                return View("/Views/NewDatabaseViews/DeleteNewAddress.cshtml", address);
            }

            // Check if address is associated with the invoice
            var isAddressUsedByInvoice = await _context.Invoices.AnyAsync(i => i.BillingAddressId == id);
            if (isAddressUsedByInvoice)
            {
                ModelState.AddModelError(string.Empty, "Cannot delete this address because it is associated with one or more invoices.");
                return View("/Views/NewDatabaseViews/DeleteNewAddress.cshtml", address);
            }

            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        private bool AddressExists(Guid id)
        {
            return _context.Addresses.Any(e => e.AddressId == id);
        }
    }
}
