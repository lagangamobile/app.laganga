using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public class Producto
{
    public string CodigoItem { get; set; } = string.Empty;
    public string NumeroItem { get; set; } = string.Empty;
    public string CodFabrica { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public CustomAttributes Grupo { get; set; }
    public CustomAttributes Marca { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal TasaIva { get; set; }
    public bool Regalo { get; set; }
    public bool Bloqueo { get; set; }
    public string CodigoArtProveedor { get; set; } = string.Empty;
    public string CodigoAlterno { get; set; } = string.Empty;
    public int AplicaPromoComisionTc { get; set; }
    public decimal ProductoComisionTc { get; set; }
    public bool OblNumser { get; set; }
    public int NCompo { get; set; }
}