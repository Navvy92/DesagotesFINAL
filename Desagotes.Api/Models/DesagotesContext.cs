using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Desagotes.Api.Models;

public partial class DesagotesContext : DbContext
{
    public DesagotesContext(DbContextOptions<DesagotesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<AccesoLog> AccesoLogs { get; set; }

    public virtual DbSet<Camionero> Camioneros { get; set; }

    public virtual DbSet<Jornada> Jornada { get; set; }

    public virtual DbSet<Remito> Remitos { get; set; }

    public virtual DbSet<Vehiculo> Vehiculos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("admin_pkey");

            entity.ToTable("admin");

            entity.HasIndex(e => e.Usuario, "admin_usuario_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Usuario).HasColumnName("usuario");
        });

        modelBuilder.Entity<Camionero>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("camionero_pkey");

            entity.ToTable("camionero");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.PinHash).HasColumnName("pin_hash");
            entity.Property(e => e.PinVersion).HasColumnName("pin_version");
            entity.Property(e => e.IntentosFallidos).HasColumnName("intentos_fallidos");
            entity.Property(e => e.BloqueadoHasta).HasColumnName("bloqueado_hasta");
        });

        modelBuilder.Entity<Jornada>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jornada_pkey");

            entity.ToTable("jornada");

            entity.HasIndex(e => e.CamioneroId, "uq_jornada_abierta_camionero")
                .IsUnique()
                .HasFilter("(estado = 'ABIERTA'::text)");

            entity.HasIndex(e => e.VehiculoId, "uq_jornada_abierta_vehiculo")
                .IsUnique()
                .HasFilter("(estado = 'ABIERTA'::text)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CamioneroId).HasColumnName("camionero_id");
            entity.Property(e => e.CheckinAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("checkin_at");
            entity.Property(e => e.CheckinFoto).HasColumnName("checkin_foto");
            entity.Property(e => e.CheckinLat).HasColumnName("checkin_lat");
            entity.Property(e => e.CheckinLng).HasColumnName("checkin_lng");
            entity.Property(e => e.CheckinPrecisionM).HasColumnName("checkin_precision_m");
            entity.Property(e => e.CheckoutAt).HasColumnName("checkout_at");
            entity.Property(e => e.CheckoutFoto).HasColumnName("checkout_foto");
            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'ABIERTA'::text")
                .HasColumnName("estado");
            entity.Property(e => e.RemitosDeclarados).HasColumnName("remitos_declarados");
            entity.Property(e => e.VehiculoId).HasColumnName("vehiculo_id");

            entity.HasOne(d => d.Camionero).WithOne(p => p.Jornadum)
                .HasForeignKey<Jornada>(d => d.CamioneroId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("jornada_camionero_id_fkey");

            entity.HasOne(d => d.Vehiculo).WithOne(p => p.Jornadum)
                .HasForeignKey<Jornada>(d => d.VehiculoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("jornada_vehiculo_id_fkey");

            entity.Property(e => e.CierreAdmin).HasColumnName("cierre_admin");
            entity.Property(e => e.CierreAdminPor).HasColumnName("cierre_admin_por");
            entity.Property(e => e.CierreAdminAt).HasColumnName("cierre_admin_at")
                  .HasColumnType("timestamp with time zone");
            entity.Property(e => e.NotaAdmin).HasColumnName("nota_admin");
        });

        modelBuilder.Entity<Remito>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("remito_pkey");

            entity.ToTable("remito");

            entity.HasIndex(e => e.NroPedido, "ix_remito_nro_pedido");

            entity.HasIndex(e => new { e.Talonario, e.NroRemito }, "uq_remito_numero").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CargadoAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("cargado_at");
            entity.Property(e => e.Cliente).HasColumnName("cliente");
            entity.Property(e => e.FotoCamara).HasColumnName("foto_camara");
            entity.Property(e => e.FotoRemito).HasColumnName("foto_remito");
            entity.Property(e => e.JornadaId).HasColumnName("jornada_id");
            entity.Property(e => e.NroPedido).HasColumnName("nro_pedido");
            entity.Property(e => e.NroRemito).HasColumnName("nro_remito");
            entity.Property(e => e.Talonario).HasColumnName("talonario");

            entity.HasOne(d => d.Jornada).WithMany(p => p.Remitos)
                .HasForeignKey(d => d.JornadaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("remito_jornada_id_fkey");
        });

        modelBuilder.Entity<Vehiculo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vehiculo_pkey");

            entity.ToTable("vehiculo");

            entity.HasIndex(e => e.Patente, "vehiculo_patente_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Patente).HasColumnName("patente");
        });
        modelBuilder.Entity<AccesoLog>(entity =>
        {
            entity.ToTable("acceso_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CamioneroId).HasColumnName("camionero_id");
            entity.Property(e => e.Evento).HasColumnName("evento");
            entity.Property(e => e.Ip).HasColumnName("ip");
            entity.Property(e => e.Dispositivo).HasColumnName("dispositivo");
            entity.Property(e => e.CreadoAt).HasColumnName("creado_at");
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
