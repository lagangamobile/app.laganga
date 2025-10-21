using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public class ProductoItem
{
    public string? seqCompte { get; set; }
    public int codigoItem { get; set; }
    public string? descripcion { get; set; }
    public decimal cantidad { get; set; }
}