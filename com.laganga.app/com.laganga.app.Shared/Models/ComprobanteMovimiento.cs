using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IComprobanteMovimiento
{
    string SeqCompte { get; set; }
    string NumCompte { get; set; }
    string Bodega { get; set; }
    DateTime Fecha { get; set; }
    List<DetalleMovimiento> Detalles { get; set; }
}

public class ComprobanteMovimiento : IComprobanteMovimiento
{
    public string SeqCompte { get; set; }
    public string NumCompte { get; set; }
    public string Bodega { get; set; }
    public DateTime Fecha { get; set; }
    public List<DetalleMovimiento> Detalles { get; set; }
}

