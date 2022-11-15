using Iida.Shared.Models;

using Microsoft.EntityFrameworkCore;

namespace Iida.Core.Contexts;

public class AppDbContext : DbContext {
	private readonly string _connectionString;
	public AppDbContext(string connectionString) => _connectionString = connectionString;
	public DbSet<Order>? Orders { get; set; }
	public DbSet<EvapotranspirationMap>? EvapotranspirationMaps { get; set; }
	public DbSet<MeteorologicalData>? MeteorologicalDatas { get; set; }
	public DbSet<SatelliteImage>? SatelliteImages { get; set; }
	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => _ = optionsBuilder.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));
}