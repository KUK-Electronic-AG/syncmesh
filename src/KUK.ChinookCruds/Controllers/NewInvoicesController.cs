using AutoMapper;
using KUK.ChinookCruds.ViewModels;
using KUK.Common.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KUK.ChinookCruds.Controllers
{
    public class NewInvoicesController : Controller
    {
        private readonly Chinook2Context _context;
        private readonly IMapper _mapper;

        public NewInvoicesController(
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
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .OrderByDescending(i => _context.InvoiceMappings
                         .Where(im => im.NewInvoiceId == i.InvoiceId)
                         .Max(im => (DateTime?)im.MappingTimestamp) ?? DateTime.MaxValue)
                    .ToListAsync();
                return View("/Views/NewDatabaseViews/NewInvoices.cshtml", invoices);
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

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceLines)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = _mapper.Map<NewInvoiceDetailsViewModel>(invoice);

            return View("/Views/NewDatabaseViews/DetailsNewInvoice.cshtml", viewModel);
        }

        // GET: NewInvoices/Create
        public IActionResult Create()
        {
            var viewModel = new NewInvoiceCreateViewModel
            {
                InvoiceDate = DateTime.Now,
                InvoiceLines = new List<NewInvoiceLineCreateViewModel>
                {
                    new NewInvoiceLineCreateViewModel { TrackId = 1, UnitPrice = 0.99m, Quantity = 1 },
                    new NewInvoiceLineCreateViewModel { TrackId = 2, UnitPrice = 1.99m, Quantity = 1 }
                }
            };
            return View("/Views/NewDatabaseViews/CreateNewInvoice.cshtml", viewModel);
        }

        // POST: NewInvoices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,InvoiceDate,BillingAddressId,Total,InvoiceLines")] NewInvoiceCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var customerExists = await _context.Customers.AnyAsync(c => c.CustomerId == viewModel.CustomerId);
                if (!customerExists)
                {
                    ModelState.AddModelError("CustomerId", "Invalid Customer ID.");
                    return View("/Views/NewDatabaseViews/CreateNewInvoice.cshtml", viewModel);
                }

                var invoice = _mapper.Map<KUK.Common.ModelsNewSchema.Invoice>(viewModel);
                invoice.InvoiceLines = _mapper.Map<List<KUK.Common.ModelsNewSchema.InvoiceLine>>(viewModel.InvoiceLines);

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
            return View("/Views/NewDatabaseViews/CreateNewInvoice.cshtml", viewModel);
        }

        public async Task<IActionResult> Edit(Guid? id)
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

            var viewModel = _mapper.Map<NewInvoiceEditViewModel>(invoice);
            return View("/Views/NewDatabaseViews/EditNewInvoice.cshtml", viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("InvoiceId,CustomerId,InvoiceDate,BillingAddressId,Total,InvoiceLines")] NewInvoiceEditViewModel viewModel)
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
            return View("/Views/NewDatabaseViews/EditNewInvoice.cshtml", viewModel);
        }

        public async Task<IActionResult> Delete(Guid? id)
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

            return View("/Views/NewDatabaseViews/DeleteNewInvoice.cshtml", invoice);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
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

        private bool InvoiceExists(Guid id)
        {
            return _context.Invoices.Any(e => e.InvoiceId == id);
        }
    }
}
