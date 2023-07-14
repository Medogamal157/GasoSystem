using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Gazify.Controllers
{
    [Route("api/GasSensor")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;

        public ValuesController(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        [Route("GetAll")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<Gas>> GetAllReads()
        {
            return Ok(_context.Gas);
        }
        
        [HttpGet]
        [Route("Get")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult GetLastRead()
        {
            return Ok(_context.Gas.OrderByDescending(g => g.CreatedOn).First());
        }

        [HttpPost]
        [Route("AddLeaked")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<Gas> Create([FromBody] Gas model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);


            Gas gas = _mapper.Map<Gas>(model);

            gas.Id = Guid.NewGuid();
            gas.CreatedById = "2eeb8e25-ef10-4cf8-bbe3-483c1a0eabac";
            if(string.IsNullOrEmpty(gas.Location))
                gas.Location = "Madinaty 234";

            _context.Gas.Add(gas);
            _context.SaveChanges();

            return Ok(gas);
        }

		[HttpPost]
		[Route("UpdateLeaked")]
		[AllowAnonymous]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public ActionResult<Gas> Edit([FromBody] Gas model)
		{
            model.Id = _context.Gas.OrderByDescending(g => g.CreatedOn).First().Id;

            if (!ModelState.IsValid)
				return BadRequest(ModelState);

            var gas = _context.Gas.Find(model.Id);
            if (gas is null)
                return NotFound();
            if(model.Read > 0)
    			gas.Read = model.Read;

            if(model.Location is not null)
    			gas.Location = model.Location;

            gas.LastUpdatedOn = DateTime.Now;
			gas.LastUpdatedById = "2eeb8e25-ef10-4cf8-bbe3-483c1a0eabac";

			_context.Gas.Update(gas);
			_context.SaveChanges();

			return Ok(gas);
		}
        
        
        [HttpPut]
		[Route("StatusLeaked")]
		[AllowAnonymous]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public IActionResult ToggleStatus(Guid id)
		{
            var gas = _context.Gas.Find(id);
            if (gas == null)
                return NotFound();
            gas.IsDeleted = !gas.IsDeleted;
            gas.LastUpdatedById = "2eeb8e25-ef10-4cf8-bbe3-483c1a0eabac";
            gas.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return Ok(gas);
        }
	}
}
