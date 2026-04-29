using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using projeotel.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Threading.Tasks;

public class RezervasyonController : Controller
{
    private readonly IConfiguration _configuration;
    public RezervasyonController(IConfiguration configuration) { _configuration = configuration; }

    [HttpGet] public IActionResult Index() => View();

    [HttpGet]
    public IActionResult SeedCities()
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        string[] sehirler = { "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Amasya", "Ankara", "Antalya", "Artvin", "Aydın", "Balıkesir", "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli", "Diyarbakır", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkari", "Hatay", "Isparta", "Mersin", "İstanbul", "İzmir", "Kars", "Kastamonu", "Kayseri", "Kırklareli", "Kırşehir", "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Kahramanmaraş", "Mardin", "Muğla", "Muş", "Nevşehir", "Niğde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Tekirdağ", "Tokat", "Trabzon", "Tunceli", "Şanlıurfa", "Uşak", "Van", "Yozgat", "Zonguldak", "Aksaray", "Bayburt", "Karaman", "Kırıkkale", "Batman", "Şırnak", "Bartın", "Ardahan", "Iğdır", "Yalova", "Karabük", "Kilis", "Osmaniye", "Düzce" };
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            foreach (var s in sehirler)
            {
                SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM sehir WHERE sehir_ad = @ad", conn);
                checkCmd.Parameters.AddWithValue("@ad", s);
                if ((int)checkCmd.ExecuteScalar() == 0)
                {
                    SqlCommand insCmd = new SqlCommand("INSERT INTO sehir (sehir_ad) VALUES (@ad)", conn);
                    insCmd.Parameters.AddWithValue("@ad", s);
                    insCmd.ExecuteNonQuery();
                }
            }
        }
        return Content("81 Sehir başarıyla kontrol edildi/eklendi.");
    }

    [HttpGet]
    public IActionResult GetSehirler()
    {
        var sehirler = new List<object>();
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            SqlCommand cmd = new SqlCommand("SELECT sehir_id, sehir_ad FROM sehir ORDER BY sehir_ad", conn);
            conn.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read()) sehirler.Add(new { id = dr["sehir_id"], ad = dr["sehir_ad"].ToString() });
        }
        return Json(sehirler);
    }

    [HttpGet]
    public IActionResult GetOtellerBySehir(int sehirId)
    {
        var oteller = new List<object>();
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            SqlCommand cmd = new SqlCommand("SELECT otel_id, otel_ad, kat_sayisi FROM otel WHERE sehir_id = @s_id", conn);
            cmd.Parameters.AddWithValue("@s_id", sehirId);
            conn.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read()) oteller.Add(new { id = dr["otel_id"], ad = dr["otel_ad"].ToString(), katSayisi = dr["kat_sayisi"] });
        }
        return Json(oteller);
    }

    [HttpGet]
    public IActionResult GetMusaitlik(DateTime giris, DateTime cikis, int otelId)
    {
        var odalar = new List<object>();
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            string sql = @"SELECT b.bungalov_id, b.oda_no, b.kapasite, b.fiyat, b.cephe,
                          (SELECT COUNT(*) FROM rezervasyon r 
                           WHERE r.bungalov_id = b.bungalov_id AND r.durum IN ('Aktif', 'OdaKapatildi')
                           AND r.giris_tarihi < @cikis AND r.cikis_tarihi > @giris) as MevcutDoluluk,
                          (SELECT COUNT(*) FROM rezervasyon r 
                           WHERE r.bungalov_id = b.bungalov_id AND r.durum = 'OdaKapatildi'
                           AND r.giris_tarihi < @cikis AND r.cikis_tarihi > @giris) as IsPrivate
                          FROM bungalov b WHERE b.otel_id = @otelId ORDER BY b.oda_no ASC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@giris", SqlDbType.Date).Value = giris.Date;
            cmd.Parameters.Add("@cikis", SqlDbType.Date).Value = cikis.Date;
            cmd.Parameters.AddWithValue("@otelId", otelId);
            conn.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                int doluluk = Convert.ToInt32(dr["MevcutDoluluk"]);
                int kapasite = Convert.ToInt32(dr["kapasite"]);
                odalar.Add(new { id = dr["bungalov_id"], odaNo = dr["oda_no"].ToString(), kat = Convert.ToInt32(dr["oda_no"]) / 1000, kap = kapasite, dolu = (Convert.ToInt32(dr["IsPrivate"]) > 0 ? kapasite : doluluk), fiyat = Convert.ToDecimal(dr["fiyat"]).ToString("F2", System.Globalization.CultureInfo.InvariantCulture), cephe = dr["cephe"].ToString() });
            }
        }
        return Json(odalar);
    }

    [HttpGet]
    public async Task<IActionResult> GetHavaDurumu(string sehir)
    {
        try
        {
            using var http = new HttpClient();
            var url = $"https://wttr.in/{Uri.EscapeDataString(sehir)}?format=j1&lang=tr";
            var json = await http.GetStringAsync(url);
            var obj = System.Text.Json.JsonDocument.Parse(json);
            var current = obj.RootElement.GetProperty("current_condition")[0];
            var temp = current.GetProperty("temp_C").GetString();
            var desc = current.GetProperty("lang_tr")[0].GetProperty("value").GetString();
            var humidity = current.GetProperty("humidity").GetString();
            var wind = current.GetProperty("windspeedKmph").GetString();
            return Json(new { success = true, temp, desc, humidity, wind, sehir });
        }
        catch
        {
            return Json(new { success = false });
        }
    }

    [HttpPost]
    public IActionResult Index(Rezervasyon model, bool odayiKapat, string loggedInUsername, string loggedInEmail)
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='rezervasyon' AND COLUMN_NAME='kullanici_ad')
                ALTER TABLE rezervasyon ADD kullanici_ad nvarchar(100) NULL", conn).ExecuteNonQuery();
            using (SqlTransaction transaction = conn.BeginTransaction())
            {
                try {
                    bool hasTc = !string.IsNullOrWhiteSpace(model.TcNo) && model.TcNo != "00000000000";
                    int musteriId;
                    if (hasTc)
                    {
                        string sql = @"IF NOT EXISTS (SELECT 1 FROM musteri WHERE tc_no = @tc)
                            INSERT INTO musteri (ad, soyad, telefon, tc_no, email) VALUES (@ad, @soyad, @tel, @tc, @mail);
                            SELECT musteri_id FROM musteri WHERE tc_no = @tc";
                        SqlCommand cmd = new SqlCommand(sql, conn, transaction);
                        cmd.Parameters.AddWithValue("@ad",    (object)model.Ad     ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@soyad", (object)model.Soyad  ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tel",   (object)model.Telefon ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tc",    model.TcNo);
                        cmd.Parameters.AddWithValue("@mail",  (object)model.Email  ?? DBNull.Value);
                        musteriId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    else
                    {
                        string findSql = @"SELECT TOP 1 musteri_id FROM musteri WHERE (ad = @ad AND soyad = @soyad AND email = @mail) ORDER BY musteri_id ASC";
                        SqlCommand findCmd = new SqlCommand(findSql, conn, transaction);
                        findCmd.Parameters.AddWithValue("@ad",    model.Ad    ?? "");
                        findCmd.Parameters.AddWithValue("@soyad", model.Soyad ?? "");
                        findCmd.Parameters.AddWithValue("@mail",  model.Email ?? "");
                        var found = findCmd.ExecuteScalar();
                        if (found != null && found != DBNull.Value)
                        {
                            musteriId = Convert.ToInt32(found);
                        }
                        else
                        {
                            string insSql = @"INSERT INTO musteri (ad, soyad, telefon, tc_no, email) OUTPUT INSERTED.musteri_id VALUES (@ad, @soyad, @tel, @tc, @mail)";
                            SqlCommand insCmd = new SqlCommand(insSql, conn, transaction);
                            insCmd.Parameters.AddWithValue("@ad",    (object)model.Ad     ?? DBNull.Value);
                            insCmd.Parameters.AddWithValue("@soyad", (object)model.Soyad  ?? DBNull.Value);
                            insCmd.Parameters.AddWithValue("@tel",   (object)model.Telefon ?? DBNull.Value);
                            insCmd.Parameters.AddWithValue("@tc",    (object)model.TcNo   ?? DBNull.Value);
                            insCmd.Parameters.AddWithValue("@mail",  (object)model.Email  ?? DBNull.Value);
                            musteriId = Convert.ToInt32(insCmd.ExecuteScalar());
                        }
                    }
                    int gun = Math.Max(1, (model.CikisTarihi.Date - model.GirisTarihi.Date).Days);
                    string durum = odayiKapat ? "OdaKapatildi" : "Aktif";
                    SqlCommand cntCmd;
                    if (!string.IsNullOrWhiteSpace(loggedInUsername))
                    {
                        cntCmd = new SqlCommand(@"SELECT COUNT(*) FROM rezervasyon r
                            JOIN musteri m ON r.musteri_id = m.musteri_id
                            WHERE r.durum IN ('Aktif','OdaKapatildi','Açığa Alındı','Kullanıldı')
                            AND (r.kullanici_ad = @uname OR m.ad = @uname OR (@mail <> '' AND m.email = @mail))", conn, transaction);
                        cntCmd.Parameters.AddWithValue("@uname", loggedInUsername);
                        cntCmd.Parameters.AddWithValue("@mail", string.IsNullOrWhiteSpace(loggedInEmail) ? "" : loggedInEmail);
                    }
                    else
                    {
                        cntCmd = new SqlCommand(@"SELECT COUNT(*) FROM rezervasyon r
                            JOIN musteri m ON r.musteri_id = m.musteri_id
                            WHERE r.durum IN ('Aktif','OdaKapatildi','Açığa Alındı','Kullanıldı')
                            AND ((@tc <> '' AND @tc <> '00000000000' AND m.tc_no = @tc) OR (@mail <> '' AND m.email = @mail))", conn, transaction);
                        cntCmd.Parameters.AddWithValue("@tc", string.IsNullOrWhiteSpace(model.TcNo) ? "" : model.TcNo);
                        cntCmd.Parameters.AddWithValue("@mail", string.IsNullOrWhiteSpace(model.Email) ? "" : model.Email);
                    }
                    int mevcutRezSayisi = Convert.ToInt32(cntCmd.ExecuteScalar());
                    bool ucuncuOdaIndirimi = mevcutRezSayisi > 0 && mevcutRezSayisi % 3 == 0;
                    string insRez = ucuncuOdaIndirimi
                        ? @"INSERT INTO rezervasyon (musteri_id, bungalov_id, giris_tarihi, cikis_tarihi, toplam_tutar, durum, Personel_id, kullanici_ad) OUTPUT INSERTED.rezervasyon_id VALUES (@m, @b, @g, @c, ROUND((SELECT COALESCE(fiyat,0) * @gun FROM bungalov WHERE bungalov_id = @b) * 0.85, 2), @durum, 1, @kAd)"
                        : @"INSERT INTO rezervasyon (musteri_id, bungalov_id, giris_tarihi, cikis_tarihi, toplam_tutar, durum, Personel_id, kullanici_ad) OUTPUT INSERTED.rezervasyon_id VALUES (@m, @b, @g, @c, (SELECT COALESCE(fiyat,0) * @gun FROM bungalov WHERE bungalov_id = @b), @durum, 1, @kAd)";
                    SqlCommand rCmd = new SqlCommand(insRez, conn, transaction);
                    rCmd.Parameters.AddWithValue("@m",    musteriId);
                    rCmd.Parameters.AddWithValue("@b",    model.BungalovId);
                    rCmd.Parameters.Add("@g", SqlDbType.Date).Value = model.GirisTarihi.Date;
                    rCmd.Parameters.Add("@c", SqlDbType.Date).Value = model.CikisTarihi.Date;
                    rCmd.Parameters.AddWithValue("@gun",  gun);
                    rCmd.Parameters.AddWithValue("@durum", durum);
                    rCmd.Parameters.AddWithValue("@kAd",  string.IsNullOrWhiteSpace(loggedInUsername) ? (object)DBNull.Value : loggedInUsername);
                    int rezId = Convert.ToInt32(rCmd.ExecuteScalar());
                    SqlCommand oCmd = new SqlCommand(@"INSERT INTO [odemeler] (rezervasyon_id, tutar, odeme_tarihi, durum) VALUES (@rid, (SELECT toplam_tutar FROM rezervasyon WHERE rezervasyon_id = @rid), GETDATE(), 'Ödendi')", conn, transaction);
                    oCmd.Parameters.AddWithValue("@rid", rezId);
                    oCmd.ExecuteNonQuery();
                    transaction.Commit();
                    return Json(new { success = true, discount = ucuncuOdaIndirimi });
                } catch (Exception ex) { transaction.Rollback(); return Json(new { success = false, message = ex.Message }); }
            }
        }
    }

    [HttpGet]
    public IActionResult GetDiscountStatus(string email, string username)
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            SqlCommand cCmd;
            if (!string.IsNullOrWhiteSpace(username))
            {
                cCmd = new SqlCommand(@"SELECT COUNT(*) FROM rezervasyon r
                    JOIN musteri m ON r.musteri_id = m.musteri_id
                    WHERE r.durum IN ('Aktif','OdaKapatildi','Kullanıldı','Açığa Alındı')
                    AND (r.kullanici_ad = @u OR m.ad = @u OR (@e <> '' AND m.email = @e))", conn);
                cCmd.Parameters.AddWithValue("@u", username);
                cCmd.Parameters.AddWithValue("@e", string.IsNullOrEmpty(email) ? "" : email);
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                cCmd = new SqlCommand(@"SELECT COUNT(*) FROM rezervasyon r
                    JOIN musteri m ON r.musteri_id = m.musteri_id
                    WHERE r.durum IN ('Aktif','OdaKapatildi','Kullanıldı','Açığa Alındı')
                    AND m.email = @e", conn);
                cCmd.Parameters.AddWithValue("@e", email);
            }
            else
            {
                cCmd = new SqlCommand("SELECT COUNT(*) FROM rezervasyon WHERE durum IN ('Aktif','OdaKapatildi','Kullanıldı','Açığa Alındı')", conn);
            }
            int sayi = Convert.ToInt32(cCmd.ExecuteScalar());
            bool indirimli = sayi > 0 && sayi % 3 == 0;
            return Json(new { indirimliMi = indirimli, mevcutSayi = sayi });
        }
    }

    [HttpGet]
    public IActionResult GetMyReservations(string email, string username)
    {
        var res = new List<object>();
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='rezervasyon' AND COLUMN_NAME='kullanici_ad') ALTER TABLE rezervasyon ADD kullanici_ad nvarchar(100) NULL", conn).ExecuteNonQuery();
            string sql = @"SELECT r.rezervasyon_id, r.giris_tarihi, r.cikis_tarihi, 
                           ISNULL(r.toplam_tutar, 0) as toplam_tutar, r.durum, b.oda_no, o.otel_ad, s.sehir_ad,
                           CAST(ISNULL(b.fiyat,0) * DATEDIFF(day, r.giris_tarihi, r.cikis_tarihi) AS decimal(18,2)) as orijinal_tutar
                           FROM rezervasyon r JOIN musteri m ON r.musteri_id = m.musteri_id 
                           JOIN bungalov b ON r.bungalov_id = b.bungalov_id
                           JOIN otel o ON b.otel_id = o.otel_id JOIN sehir s ON o.sehir_id = s.sehir_id
                           WHERE (@uname <> '' AND r.kullanici_ad = @uname) OR (@mail <> '' AND m.email = @mail) OR (@uname <> '' AND m.ad = @uname)
                           ORDER BY r.rezervasyon_id ASC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@mail",  string.IsNullOrEmpty(email)    ? "" : email);
            cmd.Parameters.AddWithValue("@uname", string.IsNullOrEmpty(username) ? "" : username);
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read()) res.Add(new {
                id = dr["rezervasyon_id"], giris = Convert.ToDateTime(dr["giris_tarihi"]).ToString("dd.MM.yyyy"),
                cikis = Convert.ToDateTime(dr["cikis_tarihi"]).ToString("dd.MM.yyyy"),
                tutar = Convert.ToDecimal(dr["toplam_tutar"]).ToString("N2"),
                orijinalTutar = Convert.ToDecimal(dr["orijinal_tutar"]).ToString("N2"),
                indirimli = Convert.ToDecimal(dr["toplam_tutar"]) < Convert.ToDecimal(dr["orijinal_tutar"]) - 0.01m,
                durum = dr["durum"].ToString(), odaNo = dr["oda_no"].ToString(),
                otelAd = dr["otel_ad"].ToString(), sehirAd = dr["sehir_ad"].ToString()
            });
        }
        return Json(res);
    }

    [HttpPost]
    public IActionResult CancelReservation(int id)
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            SqlCommand getCmd = new SqlCommand("SELECT toplam_tutar FROM rezervasyon WHERE rezervasyon_id = @id", conn);
            getCmd.Parameters.AddWithValue("@id", id);
            var tutarObj = getCmd.ExecuteScalar();
            decimal kesinti = 0;
            if (tutarObj != null && tutarObj != DBNull.Value)
                kesinti = Math.Round(Convert.ToDecimal(tutarObj) * 0.15m, 2);
            SqlCommand cmd = new SqlCommand("UPDATE rezervasyon SET durum = 'İptal Edildi' WHERE rezervasyon_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() > 0)
                return Json(new { success = true, message = $"İptal başarılı. %15 kesinti ({kesinti:N2} TL) uygulandı." });
        }
        return Json(new { success = false });
    }

    [HttpPost]
    public IActionResult HoldReservation(int id)
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            SqlCommand cmd = new SqlCommand("UPDATE rezervasyon SET durum = 'Açığa Alındı' WHERE rezervasyon_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() > 0) return Json(new { success = true, message = "Rezervasyon açığa alındı." });
        }
        return Json(new { success = false });
    }

    [HttpGet]
    public IActionResult SeedRooms()
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            var cmd = new SqlCommand { Connection = conn, CommandTimeout = 300 };
            cmd.CommandText = @"
                INSERT INTO bungalov (otel_id, oda_no, kapasite, fiyat, cephe, durum)
                SELECT o.otel_id,
                    CAST((k.kat * 1000 + r.odaIdx) AS nvarchar(10)),
                    CASE r.odaIdx WHEN 1 THEN 2 WHEN 2 THEN 2 WHEN 3 THEN 4 WHEN 4 THEN 4 WHEN 5 THEN 6 WHEN 6 THEN 6 ELSE 4 END,
                    CASE r.odaIdx WHEN 1 THEN 1500 WHEN 2 THEN 1800 WHEN 3 THEN 2500 WHEN 4 THEN 2800 WHEN 5 THEN 3500 WHEN 6 THEN 4000 ELSE 2200 END,
                    CASE r.odaIdx WHEN 1 THEN 'Deniz' WHEN 2 THEN 'Bahçe' WHEN 3 THEN 'Havuz' WHEN 4 THEN 'Şehir' WHEN 5 THEN 'Orman' WHEN 6 THEN 'Dağ' ELSE 'Şehir' END,
                    'Boş'
                FROM otel o
                CROSS JOIN (SELECT 1 kat UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10 UNION SELECT 11 UNION SELECT 12) k
                CROSS JOIN (SELECT 1 odaIdx UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7) r
                WHERE k.kat <= o.kat_sayisi
                AND NOT EXISTS (
                    SELECT 1 FROM bungalov b 
                    WHERE b.otel_id = o.otel_id 
                    AND b.oda_no = CAST((k.kat * 1000 + r.odaIdx) AS nvarchar(10))
                )";
            int inserted = cmd.ExecuteNonQuery();
            return Content($"Tamamlandı. {inserted} oda eklendi.");
        }
    }

    [HttpGet]
    public IActionResult GetHeldCredits(string email, string username)
    {
        var res = new List<object>();
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            string sql = @"SELECT r.rezervasyon_id, o.otel_ad, r.toplam_tutar, b.oda_no, s.sehir_ad,
                           CONVERT(varchar, r.giris_tarihi, 104) as giris, CONVERT(varchar, r.cikis_tarihi, 104) as cikis
                           FROM rezervasyon r JOIN musteri m ON r.musteri_id = m.musteri_id
                           JOIN bungalov b ON r.bungalov_id = b.bungalov_id JOIN otel o ON b.otel_id = o.otel_id
                           JOIN sehir s ON o.sehir_id = s.sehir_id
                           WHERE r.durum = 'Açığa Alındı' AND ((@e <> '' AND m.email = @e) OR (@u <> '' AND r.kullanici_ad = @u) OR (@u <> '' AND m.ad = @u))";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@e", string.IsNullOrEmpty(email)    ? "" : email);
            cmd.Parameters.AddWithValue("@u", string.IsNullOrEmpty(username) ? "" : username);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader())
                while (dr.Read()) res.Add(new { id = dr["rezervasyon_id"], otel = dr["otel_ad"].ToString(), sehir = dr["sehir_ad"].ToString(), oda = dr["oda_no"].ToString(), giris = dr["giris"].ToString(), cikis = dr["cikis"].ToString(), tutar = Convert.ToDecimal(dr["toplam_tutar"]).ToString("F0") });
        }
        return Json(res);
    }

    [HttpPost]
    public IActionResult UseHeldCredit(int oldId, Rezervasyon model, bool odayiKapat)
    {
        string connStr = _configuration.GetConnectionString("OtelDB");
        using (SqlConnection conn = new SqlConnection(connStr))
        {
            conn.Open();
            using (SqlTransaction trans = conn.BeginTransaction())
            {
                try {
                    var cmdOld = new SqlCommand("UPDATE rezervasyon SET durum = 'Kullanıldı' WHERE rezervasyon_id = @oldId", conn, trans);
                    cmdOld.Parameters.AddWithValue("@oldId", oldId);
                    cmdOld.ExecuteNonQuery();
                    string sqlNew = @"INSERT INTO rezervasyon (bungalov_id, musteri_id, giris_tarihi, cikis_tarihi, toplam_tutar, durum, Personel_id) OUTPUT INSERTED.rezervasyon_id VALUES (@b, (SELECT TOP 1 musteri_id FROM musteri WHERE email = @e), @g, @c, 0, @durum, 1)";
                    SqlCommand cmd = new SqlCommand(sqlNew, conn, trans);
                    cmd.Parameters.AddWithValue("@b", model.BungalovId);
                    cmd.Parameters.Add("@g", SqlDbType.Date).Value = model.GirisTarihi.Date;
                    cmd.Parameters.Add("@c", SqlDbType.Date).Value = model.CikisTarihi.Date;
                    cmd.Parameters.AddWithValue("@e", (object)model.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@durum", odayiKapat ? "OdaKapatildi" : "Aktif");
                    int newRezId = Convert.ToInt32(cmd.ExecuteScalar());
                    SqlCommand oCmd = new SqlCommand("INSERT INTO [odemeler] (rezervasyon_id, tutar, odeme_tarihi, durum) VALUES (@rid, 0, GETDATE(), 'Ödendi (Kredi)')", conn, trans);
                    oCmd.Parameters.AddWithValue("@rid", newRezId);
                    oCmd.ExecuteNonQuery();
                    trans.Commit();
                    return Json(new { success = true, message = "Açığa alınan biletiniz kullanıldı!" });
                } catch (Exception ex) { trans.Rollback(); return Json(new { success = false, message = ex.Message }); }
            }
        }
    }
}
