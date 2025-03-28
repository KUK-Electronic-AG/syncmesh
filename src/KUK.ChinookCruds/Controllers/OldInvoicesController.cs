using AutoMapper;
using KUK.ChinookCruds.ViewModels;
using KUK.Common.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KUK.ChinookCruds.Controllers
{
    public class OldInvoicesController : Controller
    {
        private readonly Chinook1DataChangesContext _context;
        private readonly IMapper _mapper;

        public OldInvoicesController(
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
                var invoices = await _context.Invoices.Include(i => i.Customer).OrderByDescending(orderby => orderby.InvoiceId).ToListAsync();
                return View("/Views/OldDatabaseViews/OldInvoices.cshtml", invoices);
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

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceLines)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<OldInvoiceDetailsViewModel>(invoice);

            return View("/Views/OldDatabaseViews/DetailsOldInvoice.cshtml", viewModel);
        }

        // GET: OldInvoices/Create
        public IActionResult Create()
        {
            var viewModel = new OldInvoiceCreateViewModel
            {
                InvoiceDate = DateTime.Now,
                InvoiceLines = new List<OldInvoiceLineCreateViewModel>
            {
                new OldInvoiceLineCreateViewModel { TrackId = 1, UnitPrice = 0.99m, Quantity = 1 },
                new OldInvoiceLineCreateViewModel { TrackId = 2, UnitPrice = 1.99m, Quantity = 1 }
            }
            };
            return View("/Views/OldDatabaseViews/CreateOldInvoice.cshtml", viewModel);
        }

        // POST: OldInvoices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,InvoiceDate,BillingAddress,BillingCity,BillingState,BillingCountry,BillingPostalCode,Total,InvoiceLines")] OldInvoiceCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var customerExists = await _context.Customers.AnyAsync(c => c.CustomerId == viewModel.CustomerId);
                if (!customerExists)
                {
                    ModelState.AddModelError("CustomerId", "Invalid Customer ID.");
                    return View("/Views/OldDatabaseViews/CreateOldInvoice.cshtml", viewModel);
                }

                var invoice = _mapper.Map<KUK.Common.ModelsOldSchema.Invoice>(viewModel);
                invoice.InvoiceLines = _mapper.Map<List<KUK.Common.ModelsOldSchema.InvoiceLine>>(viewModel.InvoiceLines);

                try
                {
                    _context.Add(invoice);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", "Unable to save changes. " + ex.Message);
                }
            }
            return View("/Views/OldDatabaseViews/CreateOldInvoice.cshtml", viewModel);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceLines)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<OldInvoiceEditViewModel>(invoice);
            return View("/Views/OldDatabaseViews/EditOldInvoice.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InvoiceId,CustomerId,InvoiceDate,BillingAddress,BillingCity,BillingState,BillingCountry,BillingPostalCode,Total,Customer,InvoiceLines")] OldInvoiceEditViewModel viewModel)
        {
            if (id != viewModel.InvoiceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.InvoiceLines)
                    .FirstOrDefaultAsync(m => m.InvoiceId == id);

                if (invoice == null)
                {
                    return NotFound();
                }

                _mapper.Map(viewModel, invoice);

                try
                {
                    _context.Update(invoice);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(viewModel.InvoiceId))
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
            return View("/Views/OldDatabaseViews/EditOldInvoice.cshtml", viewModel);
        }


        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceLines)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View("/Views/OldDatabaseViews/DeleteOldInvoice.cshtml", invoice);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var invoice = await _context.Invoices
                        .Include(i => i.InvoiceLines)
                        .FirstOrDefaultAsync(m => m.InvoiceId == id);

                    if (invoice != null)
                    {
                        _context.InvoiceLines.RemoveRange(invoice.InvoiceLines);
                        _context.Entry(invoice).State = EntityState.Deleted;
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return RedirectToAction(nameof(Index));
        }


        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceId == id);
        }
    }
}
