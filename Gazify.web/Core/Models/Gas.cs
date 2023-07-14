namespace Gazify.Core.Models
{
    public class Gas : BaseModel
    {
        public Guid Id { get; set; }

        public int Read { get; set; }
        [MaxLength(500)]
        public string? Location { get; set; }
    }
}
