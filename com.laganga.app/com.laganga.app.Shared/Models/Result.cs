using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Models;

public class Result<T>
{
    public bool succeeded { get; set; }
    public string? message { get; set; }
    public List<string>? errors { get; set; }
    public T? data { get; set; }
    public string? traceId { get; set; }
    public string? correlationId { get; set; }
}
