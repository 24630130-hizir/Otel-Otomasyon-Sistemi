using System;
using System.ComponentModel.DataAnnotations;

namespace projeotel.Models
{
    public class Rezervasyon
    {
    
        [Required]
        public string Ad { get; set; }

        [Required]
        public string Soyad { get; set; }

        [Required]
        public string Telefon { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public DateTime GirisTarihi { get; set; }

        [Required]
        public DateTime CikisTarihi { get; set; }

        [Required]
        public int BungalovId { get; set; }

        [Required]
        [StringLength(11, MinimumLength = 11)]
        public string TcNo { get; set; }

      
        public int? SehirId { get; set; }
        public int? OtelId { get; set; }
    }

    public class Bungalov
    {
        public int BungalovId { get; set; }
        public string OdaNo { get; set; }
        public int Kapasite { get; set; } 
        public decimal Fiyat { get; set; } 
        public string Durum { get; set; } 
        public string Cephe { get; set; }
        public int OtelId { get; set; }   
    }

   
    public class Otel
    {
        public int OtelId { get; set; }
        public string OtelAd { get; set; }
        public int SehirId { get; set; }
    }

    public class Sehir
    {
        public int SehirId { get; set; }
        public string SehirAd { get; set; }
    }
}