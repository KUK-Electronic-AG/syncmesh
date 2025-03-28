using AutoMapper;
using KUK.ChinookCruds.ViewModels;
using KUK.Common.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KUK.ChinookCruds.Controllers
{
    public class NewCustomersController : Controller
    {
        private readonly Chinook2Context _context;
        private readonly IMapper _mapper;

        public NewCustomersController(
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
                var customers = await _context.Customers
                    .OrderByDescending(c => _context.CustomerMappings
                         .Where(cm => cm.NewCustomerId == c.CustomerId)
                         .Max(cm => (DateTime?)cm.MappingTimestamp) ?? DateTime.MaxValue)
                    .ToListAsync();
                return View("/Views/NewDatabaseViews/NewCustomers.cshtml", customers);
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

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerId == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View("/Views/NewDatabaseViews/DetailsNewCustomer.cshtml", customer);
        }

        public IActionResult Create()
        {
            return View("/Views/NewDatabaseViews/CreateNewCustomer.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,AddressId,Phone,Email")] NewCustomerCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var customer = _mapper.Map<KUK.Common.ModelsNewSchema.Customer>(viewModel);
                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("/Views/NewDatabaseViews/CreateNewCustomer.cshtml", viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<NewCustomerEditViewModel>(customer);
            return View("/Views/NewDatabaseViews/EditNewCustomer.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("CustomerId,FirstName,LastName,AddressId,Phone,Email")] NewCustomerEditViewModel viewModel)
        {
            if (id != viewModel.CustomerId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Check if AddressId is correct
                var addressExists = await _context.Addresses.AnyAsync(a => a.AddressId == viewModel.AddressId);
                if (!addressExists)
                {
                    ModelState.AddModelError("AddressId", "Invalid Address ID.");
                    return View("/Views/NewDatabaseViews/EditNewCustomer.cshtml", viewModel);
                }

                try
                {
                    var customer = _mapper.Map<KUK.Common.ModelsNewSchema.Customer>(viewModel);
                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(viewModel.CustomerId))
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
            return View("/Views/NewDatabaseViews/EditNewCustomer.cshtml", viewModel);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerId == id);
            if (customer == null)
            {
                return NotFound();
            }

            var invoices = await _context.Invoices
                .Where(i => i.CustomerId == id)
                .ToListAsync();

            var addressId = customer.AddressId;
            var isAddressShared = await _context.Customers
                .AnyAsync(c => c.AddressId == addressId && c.CustomerId != id);

            ViewBag.Invoices = invoices;
            ViewBag.IsAddressShared = isAddressShared;

            return View("/Views/NewDatabaseViews/ConfirmDeleteNewCustomer.cshtml", customer);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id, bool deleteInvoices)
        {
            var customer = await _context.Customers.FindAsync(id);

            if (deleteInvoices)
            {
                var invoices = await _context.Invoices
                    .Where(i => i.CustomerId == id)
                    .ToListAsync();

                foreach (var invoice in invoices)
                {
                    var invoiceLines = await _context.InvoiceLines
                        .Where(il => il.InvoiceId == invoice.InvoiceId)
                        .ToListAsync();
                    _context.InvoiceLines.RemoveRange(invoiceLines);
                }

                _context.Invoices.RemoveRange(invoices);
            }

            var addressId = customer.AddressId;
            var isAddressShared = await _context.Customers
                .AnyAsync(c => c.AddressId == addressId && c.CustomerId != id);

            _context.Customers.Remove(customer);

            if (!isAddressShared)
            {
                var address = await _context.Addresses.FindAsync(addressId);
                _context.Addresses.Remove(address);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(Guid id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }
    }
}
