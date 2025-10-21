using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IOrigenMovimiento
{
    string TipoOrigen { get; set; }
    string IdentificadorOrigen { get; set; }
    List<ComprobanteMovimiento> Comprobantes { get; set; }
}

public class OrigenMovimiento
{
    public string TipoOrigen { get; set; }
    public string IdentificadorOrigen { get; set; }
    public string IdentificadorOrigenVisible { get; set; }
    public List<ComprobanteMovimiento> Comprobantes { get; set; }
}
