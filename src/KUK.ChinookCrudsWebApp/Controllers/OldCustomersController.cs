using AutoMapper;
using KUK.ChinookCrudsWebApp.ViewModels;
using KUK.ChinookSync.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KUK.ChinookCrudsWebApp.Controllers
{
    public class OldCustomersController : Controller
    {
        private readonly Chinook1DataChangesContext _context;
        private readonly IMapper _mapper;

        public OldCustomersController(
            Chinook1DataChangesContext context,
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var customers = await _context.Customers.OrderByDescending(orderby => orderby.CustomerId).ToListAsync();
                return View("/Views/OldDatabaseViews/OldCustomers.cshtml", customers);
            }
            catch (MySqlException)
            {
                return Content("Unable to connect to the database. Please try again later.");
            }
        }

        public async Task<IActionResult> Details(int? id)
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

            return View("/Views/OldDatabaseViews/DetailsOldCustomer.cshtml", customer);
        }

        public IActionResult Create()
        {
            return View("/Views/OldDatabaseViews/CreateOldCustomer.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,Address,City,State,Country,PostalCode,Phone,Email")] OldCustomerCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var customer = _mapper.Map<ChinookSync.Models.OldSchema.Customer>(viewModel);
                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View("/Views/OldDatabaseViews/CreateOldCustomer.cshtml", viewModel);
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

            var viewModel = _mapper.Map<OldCustomerEditViewModel>(customer);
            return View("/Views/OldDatabaseViews/EditOldCustomer.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("CustomerId,FirstName,LastName,Address,City,State,Country,PostalCode,Phone,Email")] OldCustomerEditViewModel viewModel)
        {
            if (id != viewModel.CustomerId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var customer = _mapper.Map<KUK.ChinookSync.Models.OldSchema.Customer>(viewModel);
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
            return View("/Views/OldDatabaseViews/EditOldCustomer.cshtml", viewModel);
        }

        public async Task<IActionResult> Delete(int? id)
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

            ViewBag.Invoices = invoices;

            return View("/Views/OldDatabaseViews/ConfirmDeleteOldCustomer.cshtml", customer);
        }


        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool deleteInvoices)
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

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }
    }
}
