using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace projeotel.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string username, string email, string password)
        {
            string connStr = _configuration.GetConnectionString("OtelDB");
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string queryUser = "INSERT INTO kullanici (ad, sifre) VALUES (@ad, @sifre)";
                            using (SqlCommand cmd = new SqlCommand(queryUser, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ad", username);
                                cmd.Parameters.AddWithValue("@sifre", password);
                                cmd.ExecuteNonQuery();
                            }

                            string queryMusteri = @"IF NOT EXISTS (SELECT 1 FROM musteri WHERE email = @mail)
                                                   INSERT INTO musteri (ad, soyad, email, telefon, tc_no) 
                                                   VALUES (@ad, 'Site Üyesi', @mail, '', '')";
                            using (SqlCommand cmd = new SqlCommand(queryMusteri, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ad", username);
                                cmd.Parameters.AddWithValue("@mail", email);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch (System.Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                return Json(new { success = true, message = "Kayıt başarılı! Müşteri paneliniz oluşturuldu." });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            string connStr = _configuration.GetConnectionString("OtelDB");
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string query = @"SELECT k.kullanici_id, k.ad, k.sifre,
                                           (SELECT TOP 1 email FROM musteri WHERE ad = k.ad AND soyad = 'Site Üyesi' ORDER BY musteri_id DESC) as email
                                    FROM kullanici k 
                                    WHERE k.ad=@ad AND k.sifre=@sifre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ad", username);
                        cmd.Parameters.AddWithValue("@sifre", password);
                        
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string email = reader["email"] != DBNull.Value ? reader["email"].ToString() : "";
                                return Json(new { success = true, name = username, username = username, email = email });
                            }
                            else
                            {
                                return Json(new { success = false, message = "Geçersiz kullanıcı adı veya şifre!" });
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Bağlantı hatası: " + ex.Message });
            }
        }
    }
}
