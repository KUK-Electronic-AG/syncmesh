//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using KUK.Common.ModelsOldSchema;
//using KUK.ChinookCruds..Models;
//using System.Threading.Tasks;

//namespace MyMVCApp.Controllers
//{
//    public class InvoicesController : Controller
//    {
//        private readonly Chinook1Context _context;

//        public InvoicesController(Chinook1Context context)
//        {
//            _context = context;
//        }

//        public async Task<IActionResult> Index()
//        {
//            var invoices = await _context.Invoices.Include(i => i.Customer).ToListAsync();
//            return View(invoices);
//        }

//        public async Task<IActionResult> Details(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var invoice = await _context.Invoices
//                .Include(i => i.Customer)
//                .FirstOrDefaultAsync(m => m.InvoiceId == id);
//            if (invoice == null)
//            {
//                return NotFound();
//            }

//            return View(invoice);
//        }

//        public IActionResult Create()
//        {
//            return View();
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create([Bind("InvoiceId,CustomerId,InvoiceDate,Total")] Invoice invoice)
//        {
//            if (ModelState.IsValid)
//            {
//                _context.Add(invoice);
//                await _context.SaveChangesAsync();
//                return RedirectToAction(nameof(Index));
//            }
//            return View(invoice);
//        }

//        public async Task<IActionResult> Edit(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var invoice = await _context.Invoices.FindAsync(id);
//            if (invoice == null)
//            {
//                return NotFound();
//            }
//            return View(invoice);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Edit(int id, [Bind("InvoiceId,CustomerId,InvoiceDate,Total")] Invoice invoice)
//        {
//            if (id != invoice.InvoiceId)
//            {
//                return NotFound();
//            }

//            if (ModelState.IsValid)
//            {
//                try
//                {
//                    _context.Update(invoice);
//                    await _context.SaveChangesAsync();
//                }
//                catch (DbUpdateConcurrencyException)
//                {
//                    if (!InvoiceExists(invoice.InvoiceId))
//                    {
//                        return NotFound();
//                    }
//                    else
//                    {
//                        throw;
//                    }
//                }
//                return RedirectToAction(nameof(Index));
//            }
//            return View(invoice);
//        }

//        public async Task<IActionResult> Delete(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var invoice = await _context.Invoices
//                .Include(i => i.Customer)
//                .FirstOrDefaultAsync(m => m.InvoiceId == id);
//            if (invoice == null)
//            {
//                return NotFound();
//            }

//            return View(invoice);
//        }

//        [HttpPost, ActionName("Delete")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DeleteConfirmed(int id)
//        {
//            var invoice = await _context.Invoices.FindAsync(id);
//            _context.Invoices.Remove(invoice);
//            await _context.SaveChangesAsync();
//            return RedirectToAction(nameof(Index));
//        }

//        private bool InvoiceExists(int id)
//        {
//            return _context.Invoices.Any(e => e.InvoiceId == id);
//        }
//    }
//}
