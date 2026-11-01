using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AerolineaVIP_MVC.Models;

namespace AerolineaVIP_MVC.Controllers
{
    // Una fila de la tabla de resultados del counter
    public class FilaCounter
    {
        public int ReservaId { get; set; }
        public string Codigo { get; set; }
        public string Vuelo { get; set; }
        public int Pasajeros { get; set; }
        public string Estado { get; set; }
    }

    // CONTROLADOR DEL COUNTER (pantalla de atención en mostrador, MOD-09).
    // Su vista es Views/Counter/Index.cshtml.
    // Los datos salen de la base AerolineaVIP (db). Los filtros llegan por la URL (GET):
    // /Counter?codigo=RV-001&documento=1712345678&estado=PENDIENTE
    // El counter NO tiene un sistema aparte: usa las mismas reservas del cliente.
    [Authorize(Roles = "Counter,Administrador,SuperUsuario")]
    public class CounterController : Controller
    {
        private AerolineaVIPEntities db = new AerolineaVIPEntities();

        public ActionResult Index(string codigo, string documento, string estado)
        {
            var filas = new List<FilaCounter>();
            try
            {
                // Se limpian los textos escritos
                codigo = (codigo ?? "").Trim();
                documento = (documento ?? "").Trim();
                estado = (estado ?? "").Trim();

                // Validaciones del servidor
                if (codigo.Length > 20)
                    ModelState.AddModelError("codigo", "El código no puede tener más de 20 caracteres.");
                if (documento.Length > 20)
                    ModelState.AddModelError("documento", "El documento no puede tener más de 20 caracteres.");

                if (ModelState.IsValid)
                {
                    // 1) Se arma la consulta y se le agregan los filtros que se llenaron
                    var consulta = db.Reserva.AsQueryable();

                    if (codigo != "")
                        consulta = consulta.Where(r => r.CodigoReserva.Contains(codigo));

                    if (estado != "")
                        consulta = consulta.Where(r => r.EstadoReservaCodigo == estado);

                    if (documento != "")
                    {
                        // Reservas donde viaja un pasajero con ese documento
                        var pasajerosIds = db.Pasajero
                            .Where(p => p.NumeroDocumento.Contains(documento))
                            .Select(p => p.PasajeroId);
                        var reservasIds = db.ReservaPasajeroVuelo
                            .Where(a => pasajerosIds.Contains(a.PasajeroId))
                            .Select(a => a.ReservaId);
                        consulta = consulta.Where(r => reservasIds.Contains(r.ReservaId));
                    }

                    // 2) Se muestran solo las 30 más recientes
                    var reservas = consulta.OrderByDescending(r => r.ReservaId).Take(30).ToList();

                    // 3) Se llena cada fila de la tabla
                    foreach (var r in reservas)
                    {
                        var asignaciones = db.ReservaPasajeroVuelo.Where(a => a.ReservaId == r.ReservaId).ToList();
                        var primera = asignaciones.FirstOrDefault();

                        filas.Add(new FilaCounter
                        {
                            ReservaId = r.ReservaId,
                            Codigo = r.CodigoReserva,
                            Vuelo = primera != null ? AyudaReservas.TextoVuelo(db, primera.VueloId) : "",
                            Pasajeros = asignaciones.Select(a => a.PasajeroId).Distinct().Count(),
                            Estado = AyudaReservas.Bonito(r.EstadoReservaCodigo)
                        });
                    }
                }

                // Datos para llenar los campos de la vista
                ViewBag.Codigo = codigo;
                ViewBag.Documento = documento;
                ViewBag.Estados = new SelectList(db.EstadoReserva.ToList(), "Codigo", "Descripcion", estado);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "No se pudo consultar las reservas: " + AyudaReservas.Mensaje(ex);
                ViewBag.Codigo = "";
                ViewBag.Documento = "";
                ViewBag.Estados = new SelectList(new List<string>());
            }
            return View(filas);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}