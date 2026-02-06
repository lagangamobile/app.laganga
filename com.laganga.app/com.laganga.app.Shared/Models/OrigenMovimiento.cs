using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public interface IOrigenMovimiento
{
    Guid? Guid { get; set; }
    string DataOrigen { get; set; }
    string TipoOrigen { get; set; }
    string IdentificadorOrigen { get; set; }
    string IdentificadorOrigenVisible { get; set; }
    string Estado { get; set; }
    string ReferenciaOrigen { get; set; }
    string TipoLogistica { get; set; }
    string ReferenciaLogistica { get; set; }
    List<ComprobanteMovimiento> Comprobantes { get; set; }

    public List<SerieMovimientoDTO> ToSeriesMovimiento();
}

public class OrigenMovimiento : IOrigenMovimiento
{
    public Guid? Guid { get; set; }
    public string DataOrigen { get; set; }
    public string TipoOrigen { get; set; }
    public string IdentificadorOrigen { get; set; }
    public string IdentificadorOrigenVisible { get; set; }
    public string Estado { get; set; }
    public string ReferenciaOrigen { get; set; }
    public string TipoLogistica { get; set; }
    public string ReferenciaLogistica { get; set; }
    public List<ComprobanteMovimiento> Comprobantes { get; set; }
    public List<SerieMovimientoDTO> ToSeriesMovimiento()
    {
        if (this.Comprobantes == null) return new List<SerieMovimientoDTO>();

        return this.Comprobantes
            // 1. Aplanamos Comprobantes -> Detalles (Manejamos nulos con ??)
            .SelectMany(comp => (comp.Detalles ?? new List<DetalleMovimiento>())
                // 2. Aplanamos Detalles -> Series (Manejamos nulos con ??)
                .SelectMany(det => (det.SeriesCapturadas ?? new List<string>())
                    // 3. Proyectamos (Creamos) el nuevo objeto SerieMovimiento
                    .Select(serie => new SerieMovimientoDTO
                    {
                        Guid = this.Guid,
                        SeqCompte = comp.SeqCompte,    // Dato del Padre
                        CodigoItem = det.CodigoItem,   // Dato del Hijo
                        Descripcion = det.Descripcion, // Dato del Hijo
                        Serie = serie                  // Dato del Nieto
                    })
                )
            ).ToList();
    }

}
