using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IOrigenMovimiento
{
    Guid? Guid { get; set; }
    string TipoOrigen { get; set; }
    string IdentificadorOrigen { get; set; }
    string IdentificadorOrigenVisible { get; set; }
    string Estado { get; set; }
    string ReferenciaOrigen { get; set; }
    string TipoLogistica { get; set; }
    string ReferenciaLogistica { get; set; }
    List<ComprobanteMovimiento> Comprobantes { get; set; }
}

public class OrigenMovimiento : IOrigenMovimiento
{
    public Guid? Guid { get; set; }
    public string TipoOrigen { get; set; }
    public string IdentificadorOrigen { get; set; }
    public string IdentificadorOrigenVisible { get; set; }
    public string Estado { get; set; }
    public string ReferenciaOrigen { get; set; }
    public string TipoLogistica { get; set; }
    public string ReferenciaLogistica { get; set; }
    public List<ComprobanteMovimiento> Comprobantes { get; set; }
}
