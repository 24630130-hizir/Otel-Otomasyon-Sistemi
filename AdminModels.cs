namespace projeotel.Models
{
    public class AdminKullanici
    {
        public int Id { get; set; }
        public string KullaniciAd { get; set; }
        public string Sifre { get; set; }
        public string Rol { get; set; } // superadmin, admin, moderator
    }

    public class AdminLoginModel
    {
        public string KullaniciAd { get; set; }
        public string Sifre { get; set; }
    }
}
