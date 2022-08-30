namespace Api
{
	public class GSPShotData
	{
		public string DeviceID { get; set; }

		public string Units { get; set; }

		public int ShotNumber { get; set; }

		public string APIversion { get; set; }

		public GSPBallData BallData { get; set; }

		public GSPClubData ClubData { get; set; }

		public GSPShotDataOptions ShotDataOptions { get; set; }
	}
}
