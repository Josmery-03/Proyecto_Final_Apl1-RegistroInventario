using GoldCrew.Data;
using GoldCrew.Data.Models;
using Microsoft.EntityFrameworkCore;
using static GoldCrew.Data.Models.Usuario;
namespace GoldCrew.Services;
public interface ICategoriaService
{
    Task<List<Categoria>> ListarAsync(string? texto = null, bool incluirInactivas = false);
    Task<Categoria?> ObtenerAsync(int id);
    Task<Categoria> CrearAsync(Categoria categoria);
    Task<Categoria> ActualizarAsync(Categoria categoria);
    Task EliminarAsync(int id);
    Task<List<ResumenCategoria>> ResumenProductosAsync();
}
public class CategoriaService : ICategoriaService
{
    private const string Modulo = "Categorías";
    private readonly IDbContextFactory<GoldCrewDbContext> _factory;
    public CategoriaService(IDbContextFactory<GoldCrewDbContext> factory) => _factory = factory;

    private static ReglaNegocioException Error(string campo, string detalle) =>
        new($"Error de validación en el apartado de {Modulo}: el campo '{campo}' {detalle}");

    public async Task<List<Categoria>> ListarAsync(string? texto = null, bool incluirInactivas = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var q = db.Categorias.AsNoTracking().AsQueryable();
        if (!incluirInactivas) q = q.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(c => c.Nombre.Contains(t) || (c.Descripcion != null && c.Descripcion.Contains(t)));
        }
        return await q.OrderBy(c => c.Nombre).ToListAsync();
    }
    public async Task<Categoria?> ObtenerAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }
    public async Task<Categoria> CrearAsync(Categoria categoria)
    {
        await using var db = await _factory.CreateDbContextAsync();
        categoria.Nombre = categoria.Nombre?.Trim() ?? "";
        categoria.Id = 0;
        categoria.Activo = true;
        await ValidarAsync(db, categoria);
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return categoria;
    }
    public async Task<Categoria> ActualizarAsync(Categoria categoria)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var actual = await db.Categorias.FindAsync(categoria.Id)
            ?? throw Error("Id", "no corresponde a una categoría existente.");
        categoria.Nombre = categoria.Nombre?.Trim() ?? "";
        await ValidarAsync(db, categoria);
        actual.Nombre = categoria.Nombre;
        actual.Descripcion = categoria.Descripcion?.Trim();
        await db.SaveChangesAsync();
        return actual;
    }

    public async Task EliminarAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var c = await db.Categorias.FindAsync(id)
            ?? throw Error("Id", "no corresponde a una categoría existente.");
        if (await db.Productos.AnyAsync(p => p.CategoriaId == id && p.Activo))
            throw new ReglaNegocioException(
                $"Error en el apartado de {Modulo}: no se puede eliminar '{c.Nombre}' porque tiene productos activos asociados.");
        c.Activo = false;                      
        await db.SaveChangesAsync();
    }
    public async Task<List<ResumenCategoria>> ResumenProductosAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categorias.AsNoTracking().Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new ResumenCategoria(
                c.Nombre,
                db.Productos.Count(p => p.CategoriaId == c.Id && p.Activo)))
            .ToListAsync();
    }
    private static async Task ValidarAsync(GoldCrewDbContext db, Categoria c)
    {
        if (string.IsNullOrWhiteSpace(c.Nombre)) throw Error("Nombre", "es obligatorio.");
        if (c.Nombre.Length > 80) throw Error("Nombre", "no puede exceder 80 caracteres.");
        if (c.Descripcion is { Length: > 250 }) throw Error("Descripción", "no puede exceder 250 caracteres.");
        if (await db.Categorias.AnyAsync(x => x.Id != c.Id && x.Nombre == c.Nombre))
            throw Error("Nombre", "ya existe (puede estar desactivada).");
    }
}