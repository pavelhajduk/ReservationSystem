namespace PSRes.Models
{
    public class ReservationEdit
    {
        public Reservation Reservation { get; set; }
        public ApplicationUser User { get; set; }
        public ReservationPoint Point { get; set; }
    }
}
