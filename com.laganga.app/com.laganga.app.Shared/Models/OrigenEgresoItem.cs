using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public class OrigenEgresoItem
{
    public string secuencia { get; set; }
    public List<ProductoItem>? productos { get; set; }
}
