namespace Bookify.Web.Controllers
{
    [Authorize]
    public class GasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        public GasController(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        [HttpGet]
        public IActionResult Index()
        {
            var gas = _context.Gas.AsNoTracking().ToList();

            return View(gas);
        }

        [HttpGet]
        [AjaxOnly]
        public IActionResult Create()
        {
            return PartialView("_Form");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Gas model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var gas = _mapper.Map<Gas>(model);
            gas.Id = Guid.NewGuid();
			gas.CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

            _context.Gas.Add(gas);
            _context.SaveChanges();

            return PartialView("_GasRow", gas);
        }

        [HttpGet]
        [AjaxOnly]
        public IActionResult Edit(Guid id)
        {
            var gas = _context.Gas.Find(id);
            if (gas is null)
                return NotFound();

            var viewModel = _mapper.Map<Gas>(gas);
            return PartialView("_Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Gas model)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            var gas = _context.Gas.Find(model.Id);
            if (gas is null)
                return NotFound();

			gas = _mapper.Map<Gas>(gas);
			gas.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
			gas.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return PartialView("_GasRow", gas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(Guid id)
        {
            var gas = _context.Gas.Find(id);
            if (gas == null)
                return NotFound();
			gas.IsDeleted = !gas.IsDeleted;
			gas.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
			gas.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return Ok(gas.LastUpdatedOn.ToString());
        }
    }
}
