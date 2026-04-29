using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using projeotel.Models;

namespace projeotel.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _cfg;
        public AdminController(IConfiguration cfg) { _cfg = cfg; }

        private string ConnStr => _cfg.GetConnectionString("OtelDB");

        private void EnsureAdminTable()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM admin_kullanici WHERE rol='superadmin')
                    INSERT INTO admin_kullanici (kullanici_adi, sifre, rol, aktif) VALUES ('superadmin', 'Admin1453!', 'superadmin', 1);", conn).ExecuteNonQuery();
            }
            catch { }
        }

        private AdminKullanici GetSessionAdmin()
        {
            var ad  = HttpContext.Session.GetString("AdminAd");
            var rol = HttpContext.Session.GetString("AdminRol");
            if (string.IsNullOrEmpty(ad)) return null;
            return new AdminKullanici { KullaniciAd = ad, Rol = rol };
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (GetSessionAdmin() == null) return RedirectToAction("Login");
            ViewBag.Admin = GetSessionAdmin();
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (GetSessionAdmin() != null) return RedirectToAction("Index");
            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult Login(string kullaniciAd, string sifre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(kullaniciAd) || string.IsNullOrWhiteSpace(sifre))
                    return Json(new { success = false, message = "Kullanıcı adı ve şifre boş olamaz." });

                EnsureAdminTable();

                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                string kulAdiKol = "kullanici_adi";
                try
                {
                    var colVal = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='admin_kullanici' AND COLUMN_NAME IN ('kullanici_adi','kullanici_ad','username')", conn).ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(colVal)) kulAdiKol = colVal;
                }
                catch { }

                var cmd = new SqlCommand($"SELECT {kulAdiKol}, rol FROM admin_kullanici WHERE {kulAdiKol}=@ad AND sifre=@s", conn);
                cmd.Parameters.AddWithValue("@ad", kullaniciAd);
                cmd.Parameters.AddWithValue("@s", sifre);

                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        HttpContext.Session.SetString("AdminAd", dr[kulAdiKol].ToString());
                        HttpContext.Session.SetString("AdminRol", dr["rol"].ToString());
                        return Json(new { success = true });
                    }
                }

                var cmd2 = new SqlCommand("SELECT ad FROM kullanici WHERE ad=@ad AND sifre=@s", conn);
                cmd2.Parameters.AddWithValue("@ad", kullaniciAd);
                cmd2.Parameters.AddWithValue("@s", sifre);

                using (var dr2 = cmd2.ExecuteReader())
                {
                    if (dr2.Read())
                    {
                        HttpContext.Session.SetString("AdminAd", dr2["ad"].ToString());
                        HttpContext.Session.SetString("AdminRol", "moderator");
                        return Json(new { success = true });
                    }
                }

                return Json(new { success = false, message = "Kullanıcı adı veya şifre hatalı!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public IActionResult LogoutAndGoHome()
        {
            HttpContext.Session.Clear();
            return Redirect("/");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult SorunBildir(string kategori, string aciklama, string kullaniciAd)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO sorun_bildiri (kategori, aciklama, kullanici_ad, tarih, okundu) VALUES (@k, @a, @u, GETDATE(), 0)", conn);
                cmd.Parameters.AddWithValue("@k", kategori ?? "Diğer");
                cmd.Parameters.AddWithValue("@a", aciklama ?? "");
                cmd.Parameters.AddWithValue("@u", kullaniciAd ?? "Misafir");
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetSorunlar()
        {
            if (GetSessionAdmin() == null) return Json(new { });
            var list = new List<object>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            using var dr = new SqlCommand("SELECT TOP 50 id, kategori, aciklama, kullanici_ad, tarih, okundu FROM sorun_bildiri ORDER BY tarih DESC", conn).ExecuteReader();
            while (dr.Read())
                list.Add(new {
                    id       = dr["id"],
                    kategori = dr["kategori"].ToString(),
                    aciklama = dr["aciklama"].ToString(),
                    kullanici= dr["kullanici_ad"].ToString(),
                    tarih    = Convert.ToDateTime(dr["tarih"]).ToString("dd.MM.yyyy HH:mm"),
                    okundu   = Convert.ToBoolean(dr["okundu"])
                });
            return Json(list);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult SorunOkundu(int id)
        {
            if (GetSessionAdmin() == null) return Json(new { success = false });
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            new SqlCommand($"UPDATE sorun_bildiri SET okundu=1 WHERE id={id}", conn).ExecuteNonQuery();
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult Setup()
        {
            var sonuclar = new List<string>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                var cols = new List<string>();
                using (var dr = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='admin_kullanici' ORDER BY ORDINAL_POSITION", conn).ExecuteReader())
                    while (dr.Read()) cols.Add(dr["COLUMN_NAME"].ToString().ToLower());
                sonuclar.Add("Kolonlar: " + string.Join(", ", cols));
                int mevcut = Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM admin_kullanici", conn).ExecuteScalar());
                sonuclar.Add("Mevcut kayıt sayısı: " + mevcut);
                string kulAdiKol = cols.Contains("kullanici_adi") ? "kullanici_adi" : cols.Contains("kullanici_ad") ? "kullanici_ad" : cols.Contains("username") ? "username" : cols[1];
                string sifreKol  = cols.Contains("sifre") ? "sifre" : cols.Contains("password") ? "password" : "sifre";
                string rolKol    = cols.Contains("rol") ? "rol" : "role";
                new SqlCommand($"DELETE FROM admin_kullanici WHERE {kulAdiKol}='Hızır'", conn).ExecuteNonQuery();
                string insertSql = cols.Contains("aktif")
                    ? $"INSERT INTO admin_kullanici ({kulAdiKol}, {sifreKol}, {rolKol}, aktif) VALUES ('Hızır', '5555', 'superadmin', 1)"
                    : $"INSERT INTO admin_kullanici ({kulAdiKol}, {sifreKol}, {rolKol}) VALUES ('Hızır', '5555', 'superadmin')";
                new SqlCommand(insertSql, conn).ExecuteNonQuery();
                sonuclar.Add("Hızır / 5555 / superadmin eklendi.");
            }
            catch (Exception ex) { sonuclar.Add("Hata: " + ex.Message); }
            return Content(string.Join("\n", sonuclar));
        }

        public IActionResult Odalar()
        {
            var admin = GetSessionAdmin();
            if (admin == null) return RedirectToAction("Login");
            ViewBag.Admin = admin;
            var list = new List<dynamic>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            using var dr = new SqlCommand(@"SELECT b.bungalov_id, b.oda_no, b.kapasite, b.fiyat, b.durum, b.cephe, o.otel_ad, s.sehir_ad
                FROM bungalov b JOIN otel o ON b.otel_id=o.otel_id JOIN sehir s ON o.sehir_id=s.sehir_id
                ORDER BY s.sehir_ad, o.otel_ad, b.oda_no", conn).ExecuteReader();
            while (dr.Read())
                list.Add(new {
                    Id       = dr["bungalov_id"],
                    OdaNo    = dr["oda_no"].ToString(),
                    Kapasite = dr["kapasite"] == DBNull.Value ? 0 : Convert.ToInt32(dr["kapasite"]),
                    Fiyat    = dr["fiyat"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["fiyat"]),
                    Durum    = dr["durum"].ToString(),
                    Cephe    = dr["cephe"] == DBNull.Value ? "" : dr["cephe"].ToString(),
                    OtelAd   = dr["otel_ad"].ToString(),
                    SehirAd  = dr["sehir_ad"].ToString()
                });
            return View(list);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult OdaDurumGuncelle(int id, string yeniDurum)
        {
            if (GetSessionAdmin() == null) return RedirectToAction("Login");
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            var cmd = new SqlCommand("UPDATE bungalov SET durum=@d WHERE bungalov_id=@id", conn);
            cmd.Parameters.AddWithValue("@d", yeniDurum);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return RedirectToAction("Odalar");
        }

        public IActionResult Rezervasyonlar()
        {
            var admin = GetSessionAdmin();
            if (admin == null) return RedirectToAction("Login");
            ViewBag.Admin = admin;
            var list = new List<dynamic>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            using var dr = new SqlCommand(@"SELECT r.rezervasyon_id,
                CONVERT(varchar,r.giris_tarihi,104) AS giris, CONVERT(varchar,r.cikis_tarihi,104) AS cikis,
                r.toplam_tutar, r.durum, ISNULL(CAST(r.rezervasyon_id AS nvarchar),'-') AS kullanici_ad,
                m.ad+' '+m.soyad AS musteri_ad, b.oda_no, o.otel_ad, s.sehir_ad
                FROM rezervasyon r JOIN musteri m ON r.musteri_id=m.musteri_id
                JOIN bungalov b ON r.bungalov_id=b.bungalov_id JOIN otel o ON b.otel_id=o.otel_id JOIN sehir s ON o.sehir_id=s.sehir_id
                ORDER BY r.rezervasyon_id DESC", conn).ExecuteReader();
            while (dr.Read())
                list.Add(new {
                    Id          = dr["rezervasyon_id"],
                    Giris       = dr["giris"].ToString(),
                    Cikis       = dr["cikis"].ToString(),
                    Tutar       = (dr["toplam_tutar"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["toplam_tutar"])).ToString("N2"),
                    Durum       = dr["durum"].ToString(),
                    KullaniciAd = dr["kullanici_ad"].ToString(),
                    MusteriAd   = dr["musteri_ad"].ToString(),
                    OdaNo       = dr["oda_no"].ToString(),
                    OtelAd      = dr["otel_ad"].ToString(),
                    SehirAd     = dr["sehir_ad"].ToString()
                });
            return View(list);
        }

        public IActionResult Kullanicilar()
        {
            var admin = GetSessionAdmin();
            if (admin == null) return RedirectToAction("Login");
            if (admin.Rol == "moderator") return RedirectToAction("Index");
            ViewBag.Admin = admin;
            var list = new List<dynamic>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            EnsureAdminTable();
            using var dr = new SqlCommand("SELECT admin_id, kullanici_adi, rol FROM admin_kullanici ORDER BY admin_id", conn).ExecuteReader();
            while (dr.Read())
                list.Add(new { Id = dr["admin_id"], KullaniciAd = dr["kullanici_adi"].ToString(), Rol = dr["rol"].ToString() });
            return View(list);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult AdminEkle(string kullaniciAd, string sifre, string rol)
        {
            var admin = GetSessionAdmin();
            if (admin == null || admin.Rol != "superadmin") return Json(new { success = false, message = "Yetkisiz işlem." });
            if (rol == "superadmin") return Json(new { success = false, message = "Süperadmin rolü eklenemez." });
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO admin_kullanici (kullanici_adi, sifre, rol) VALUES (@ad,@s,@r)", conn);
                cmd.Parameters.AddWithValue("@ad", kullaniciAd);
                cmd.Parameters.AddWithValue("@s", sifre);
                cmd.Parameters.AddWithValue("@r", rol);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult AdminSil(int id)
        {
            var admin = GetSessionAdmin();
            if (admin == null || admin.Rol != "superadmin") return Json(new { success = false, message = "Yetkisiz işlem." });
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            var rol = new SqlCommand("SELECT rol FROM admin_kullanici WHERE admin_id=@id", conn);
            rol.Parameters.AddWithValue("@id", id);
            if (rol.ExecuteScalar()?.ToString() == "superadmin") return Json(new { success = false, message = "Süperadmin silinemez." });
            var cmd = new SqlCommand("DELETE FROM admin_kullanici WHERE admin_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return Json(new { success = true });
        }

        public IActionResult Download()
        {
            var admin = GetSessionAdmin();
            if (admin == null) return RedirectToAction("Login");
            ViewBag.Admin = admin;
            return View();
        }

        [HttpGet]
        public IActionResult GetDashboardStats()
        {
            if (GetSessionAdmin() == null) return Json(new { });
            using var conn = new SqlConnection(ConnStr);
            conn.Open();
            int aktif   = Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM rezervasyon WHERE durum IN ('Aktif','OdaKapatildi')", conn).ExecuteScalar());
            int musteri = Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM musteri", conn).ExecuteScalar());
            int oda     = Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM bungalov", conn).ExecuteScalar());
            var gObj    = new SqlCommand("SELECT ISNULL(SUM(ISNULL(toplam_tutar,0)),0) FROM rezervasyon WHERE durum NOT IN ('İptal Edildi')", conn).ExecuteScalar();
            decimal gelir = (gObj != null && gObj != DBNull.Value) ? Convert.ToDecimal(gObj) : 0;
            var sonRez = new List<object>();
            using (var dr = new SqlCommand(@"SELECT TOP 10 r.rezervasyon_id, m.ad+' '+m.soyad AS musteri, o.otel_ad, r.toplam_tutar, r.durum
                FROM rezervasyon r JOIN musteri m ON r.musteri_id=m.musteri_id
                JOIN bungalov b ON r.bungalov_id=b.bungalov_id JOIN otel o ON b.otel_id=o.otel_id
                ORDER BY r.rezervasyon_id DESC", conn).ExecuteReader())
                while (dr.Read())
                    sonRez.Add(new {
                        id      = dr["rezervasyon_id"],
                        musteri = dr["musteri"].ToString(),
                        otel    = dr["otel_ad"].ToString(),
                        tutar   = (dr["toplam_tutar"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["toplam_tutar"])).ToString("N0"),
                        durum   = dr["durum"].ToString()
                    });
            var durumlar = new Dictionary<string, int>();
            using (var dr2 = new SqlCommand("SELECT durum, COUNT(*) AS sayi FROM rezervasyon GROUP BY durum", conn).ExecuteReader())
                while (dr2.Read()) durumlar[dr2["durum"].ToString()] = Convert.ToInt32(dr2["sayi"]);
            return Json(new { aktif, musteri, oda, gelir, sonRez, durumlar });
        }
    }
}
