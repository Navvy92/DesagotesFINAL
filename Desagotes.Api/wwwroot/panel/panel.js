const $ = (s) => document.querySelector(s);
const esc = (s) => String(s ?? "").replace(/[&<>"]/g,
    (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const fmt = (d) => new Date(d).toLocaleString("es-AR");
const JSON_H = { "Content-Type": "application/json" };
const cfg = { camioneros: "nombre", vehiculos: "patente" };
const VISTAS = ["buscar", "jornadas", "camioneros", "vehiculos", "exportar", "accesos"];

// ---------- base ----------
function mostrar(logueado, usuario = "") {
    $("#login").hidden = logueado;
    $("#panel").hidden = !logueado;
    $("#quien").textContent = usuario;
    $("#msg").textContent = "";
    function mostrar(logueado, usuario = "") {
        $("#login").hidden = logueado;
        $("#panel").hidden = !logueado;
        $("#quien").textContent = usuario;
        $("#msg").textContent = "";
        if (logueado) cargarListado();
    }
}

async function api(url, opciones) {
    const r = await fetch(url, opciones);
    if (r.status === 401 && !url.includes("/auth/login")) {
        mostrar(false);
        throw new Error("Tu sesión venció. Ingresá de nuevo.");
    }
    const datos = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(datos.mensaje || "Error " + r.status);
    return datos;
}

// ---------- login ----------
$("#btn-login").onclick = async () => {
    try {
        const r = await api("/api/auth/login", {
            method: "POST", headers: JSON_H,
            body: JSON.stringify({ usuario: $("#usuario").value, password: $("#clave").value })
        });
        $("#clave").value = "";
        mostrar(true, r.usuario);
    } catch (e) { $("#msg").textContent = e.message; }
};

$("#btn-salir").onclick = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    $("#resultados").innerHTML = "";
    mostrar(false);
};

// ---------- navegación entre vistas ----------
document.querySelectorAll("[data-vista]").forEach((b) => {
    b.onclick = () => {
        const vista = b.dataset.vista;
        document.querySelectorAll("[data-vista]").forEach((x) => x.classList.toggle("sec", x !== b));
        for (const v of VISTAS) $("#v-" + v).hidden = v !== vista;
        $("#msg").textContent = "";
        if (cfg[vista]) listar(vista);
        if (vista === "jornadas") cargarJornadas();
        if (vista === "accesos") cargarAccesos();
        if (vista === "buscar") cargarListado();
    };
});

// ---------- buscar remitos ----------
$("#btn-buscar").onclick = async () => {
    $("#msg").textContent = "";
    const p = new URLSearchParams();
    if ($("#b-talonario").value) p.set("talonario", $("#b-talonario").value);
    if ($("#b-numero").value) p.set("nroRemito", $("#b-numero").value);
    if ($("#b-pedido").value.trim()) p.set("pedido", $("#b-pedido").value.trim());

    try {
        const lista = await api("/api/remitos/buscar?" + p);
        $("#resultados").innerHTML = lista.length
            ? lista.map((r) => `
        <div class="tarjeta">
          <strong>Remito ${esc(r.remito)}</strong> · ${esc(r.cliente)} · Pedido ${esc(r.nroPedido ?? "pendiente")}
          <div>${new Date(r.fecha).toLocaleDateString("es-AR")} · ${esc(r.camionero)} · ${esc(r.patente)}</div>
          <div class="fotos">
            <a href="${esc(r.urlFotoRemito)}" target="_blank"><img loading="lazy" src="${esc(r.urlFotoRemito)}" alt="Remito"></a>
            <a href="${esc(r.urlFotoCamara)}" target="_blank"><img loading="lazy" src="${esc(r.urlFotoCamara)}" alt="Cámara desagotada"></a>
          </div>
        </div>`).join("")
            : "<p>Sin resultados.</p>";
    } catch (e) { $("#msg").textContent = e.message; }
};

// ---------- ABM camioneros y camiones ----------
async function listar(tipo) {
    const campo = cfg[tipo];
    try {
        const datos = await api("/api/admin/" + tipo);
        $("#l-" + tipo).innerHTML = datos.map((d) => `
      <div class="tarjeta fila">
        <strong style="flex:1;${d.activo ? "" : "opacity:.5"}">${esc(d[campo])}${d.activo ? "" : " (de baja)"}${tipo === "camioneros" && !d.tienePin ? " · sin PIN" : ""}</strong>
        ${tipo === "camioneros" ? `<button class="sec" data-acc="pin" data-tipo="${tipo}" data-id="${d.id}" data-valor="${esc(d.nombre)}">Generar PIN</button>` : ""}
        <button class="sec" data-acc="editar" data-tipo="${tipo}" data-id="${d.id}" data-valor="${esc(d[campo])}">Editar</button>
        <button class="sec" data-acc="activo" data-tipo="${tipo}" data-id="${d.id}" data-activo="${d.activo}">${d.activo ? "Dar de baja" : "Reactivar"}</button>
      </div>`).join("");
    } catch (e) { $("#msg").textContent = e.message; }
}

document.querySelectorAll("[data-alta]").forEach((b) => {
    b.onclick = async () => {
        const tipo = b.dataset.alta;
        const input = $("#n-" + tipo);
        try {
            await api("/api/admin/" + tipo, {
                method: "POST", headers: JSON_H,
                body: JSON.stringify({ [cfg[tipo]]: input.value })
            });
            input.value = "";
            $("#msg").textContent = "";
            listar(tipo);
        } catch (e) { $("#msg").textContent = e.message; }
    };
});

// ---------- jornadas: cierres administrativos ----------
async function cargarJornadas() {
    try {
        const [ab, pe] = await Promise.all([
            api("/api/admin/jornadas/abiertas"),
            api("/api/jornadas/pendientes")
        ]);

        $("#l-abiertas").innerHTML = ab.length ? ab.map((j) => `
      <div class="tarjeta">
        <strong>${esc(j.camionero)} · ${esc(j.patente)}</strong>
        <div>Entrada: ${fmt(j.checkinAt)} (${j.horas} hs abierta)</div>
        <button data-cierre="cerrar" data-id="${j.id}" data-nombre="${esc(j.camionero)}">Cerrar jornada</button>
      </div>`).join("") : "<p>No hay jornadas abiertas.</p>";

        $("#l-pendientes").innerHTML = pe.length ? pe.map((j) => {
            const plazo = j.horasRestantes == null ? "sin plazo"
                : j.vencida ? "⚠️ vencida" : `quedan ${j.horasRestantes} hs`;
            return `
      <div class="tarjeta">
        <strong>${esc(j.camionero)} · ${esc(j.patente)}</strong>
        <div>Salida: ${fmt(j.checkoutAt)} · Cargados ${j.cargados} de ${j.declarados} · ${plazo}</div>
        <div class="fila">
          <button class="sec" data-ver="${j.id}">Ver remitos</button>
          <button data-cierre="validar" data-id="${j.id}" data-nombre="${esc(j.camionero)}">Validar manualmente</button>
        </div>
        <div id="rem-${j.id}"></div>
      </div>`;
        }).join("") : "<p>No hay jornadas pendientes.</p>";
    } catch (e) { $("#msg").textContent = e.message; }
}

$("#c-cancelar").onclick = () => { $("#cierre-form").hidden = true; };

$("#c-ok").onclick = async () => {
    const id = $("#c-id").value;
    const modo = $("#c-modo").value;
    try {
        if (modo === "cerrar") {
            if ($("#c-remitos").value === "") throw new Error("Indicá cuántos remitos hizo.");
            const hora = $("#c-hora").value;
            await api(`/api/admin/jornadas/${id}/cerrar`, {
                method: "POST", headers: JSON_H,
                body: JSON.stringify({
                    remitosDeclarados: Number($("#c-remitos").value),
                    horaSalida: hora ? new Date(hora).toISOString() : null,
                    nota: $("#c-nota").value
                })
            });
        } else {
            await api(`/api/admin/jornadas/${id}/validar`, {
                method: "POST", headers: JSON_H,
                body: JSON.stringify({ nota: $("#c-nota").value })
            });
        }
        $("#cierre-form").hidden = true;
        $("#msg").textContent = "";
        cargarJornadas();
    } catch (err) { $("#msg").textContent = err.message; }
};

// ---------- clics delegados (botones generados dinámicamente) ----------
document.addEventListener("click", async (e) => {
    // Editar / dar de baja / reactivar
    const acc = e.target.closest("[data-acc]");
    if (acc) {
        const { acc: accion, tipo, id } = acc.dataset;
        try {
            if (accion === "pin") {
                if (!confirm(`¿Generar un PIN nuevo para ${acc.dataset.valor}? Se cierran sus sesiones abiertas.`)) return;
                const r = await api(`/api/admin/camioneros/${id}/pin`, { method: "POST" });
                alert(`PIN de ${acc.dataset.valor}: ${r.pin}\n\nAnotalo ahora: no se vuelve a mostrar.`);
                listar(tipo);
                return;
            }
            if (accion === "editar") {
                const nuevo = prompt("Nuevo valor:", acc.dataset.valor);
                if (nuevo === null) return;
                await api(`/api/admin/${tipo}/${id}`, {
                    method: "PUT", headers: JSON_H,
                    body: JSON.stringify({ [cfg[tipo]]: nuevo })
                });
            } else {
                await api(`/api/admin/${tipo}/${id}/activo`, {
                    method: "PATCH", headers: JSON_H,
                    body: JSON.stringify({ activo: acc.dataset.activo !== "true" })
                });
            }
            $("#msg").textContent = "";
            listar(tipo);
        } catch (err) { $("#msg").textContent = err.message; }
        return;
    }

    // Ver remitos de una jornada pendiente
    const ver = e.target.closest("[data-ver]");
    if (ver) {
        try {
            const rs = await api(`/api/admin/jornadas/${ver.dataset.ver}/remitos`);
            $("#rem-" + ver.dataset.ver).innerHTML = rs.length ? rs.map((r) => `
        <div>
          <strong>${esc(r.remito)}</strong> · ${esc(r.cliente)} · Pedido ${esc(r.nroPedido ?? "pendiente")}
          <div class="fotos">
            <a href="${esc(r.urlFotoRemito)}" target="_blank"><img loading="lazy" src="${esc(r.urlFotoRemito)}" alt="Remito"></a>
            <a href="${esc(r.urlFotoCamara)}" target="_blank"><img loading="lazy" src="${esc(r.urlFotoCamara)}" alt="Cámara"></a>
          </div>
        </div>`).join("") : "<p>Sin remitos cargados.</p>";
        } catch (err) { $("#msg").textContent = err.message; }
        return;
    }

    // Abrir el formulario de cierre / validación
    const c = e.target.closest("[data-cierre]");
    if (c) {
        const cerrar = c.dataset.cierre === "cerrar";
        $("#c-id").value = c.dataset.id;
        $("#c-modo").value = c.dataset.cierre;
        $("#cierre-titulo").textContent =
            (cerrar ? "Cerrar jornada de " : "Validar manualmente la jornada de ") + c.dataset.nombre;
        $("#c-campos-cierre").hidden = !cerrar;
        $("#c-remitos").value = "";
        $("#c-hora").value = "";
        $("#c-nota").value = "";
        $("#cierre-form").hidden = false;
        $("#cierre-form").scrollIntoView();
    }
});
// ---------- exportar a Excel ----------
function fechaISO(restarDias = 0) {
    const d = new Date();
    d.setDate(d.getDate() - restarDias);
    return d.toLocaleDateString("sv-SE");   // formato AAAA-MM-DD en hora local
}
$("#e-desde").value = fechaISO(7);
$("#e-hasta").value = fechaISO();

$("#btn-exportar").onclick = async () => {
    const boton = $("#btn-exportar");
    boton.disabled = true;
    try {
        const p = new URLSearchParams({ desde: $("#e-desde").value, hasta: $("#e-hasta").value });
        if ($("#e-cliente").value) p.set("cliente", $("#e-cliente").value);

        const r = await fetch("/api/admin/exportar/remitos?" + p);
        if (r.status === 401) { mostrar(false); throw new Error("Tu sesión venció. Ingresá de nuevo."); }
        if (!r.ok) {
            const d = await r.json().catch(() => ({}));
            throw new Error(d.mensaje || "Error " + r.status);
        }

        const blob = await r.blob();
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `remitos_${$("#e-desde").value}_${$("#e-hasta").value}.xlsx`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(a.href), 1000);
        $("#msg").textContent = "";
    } catch (e) { $("#msg").textContent = e.message; }
    finally { boton.disabled = false; }
};
// ---------- listado de remitos cargados ----------
async function cargarListado() {
    try {
        const lista = await api("/api/remitos/listado");
        $("#listado").innerHTML = lista.length
            ? `<table>
                 <thead><tr><th>Remito</th><th>Camionero</th><th>Fecha</th></tr></thead>
                 <tbody>${lista.map((r) => `
                   <tr data-rem-t="${esc(r.talonario)}" data-rem-n="${esc(r.nroRemito)}">
                     <td><strong>${esc(r.remito)}</strong></td>
                     <td>${esc(r.camionero)}</td>
                     <td>${new Date(r.fecha).toLocaleDateString("es-AR")}</td>
                   </tr>`).join("")}</tbody>
               </table>`
            : "<p>Todavía no hay remitos cargados.</p>";
    } catch (e) { /* si falla, el panel sigue funcionando sin el listado */ }
}

// Tocar una fila: busca ese remito y muestra sus fotos
document.addEventListener("click", (e) => {
    const fila = e.target.closest("[data-rem-t]");
    if (!fila) return;
    $("#b-talonario").value = fila.dataset.remT;
    $("#b-numero").value = fila.dataset.remN;
    $("#b-pedido").value = "";
    $("#btn-buscar").click();
    window.scrollTo({ top: 0, behavior: "smooth" });
});
// ---------- accesos de camioneros ----------
async function cargarAccesos() {
    try {
        const sel = $("#a-camionero");
        if (sel.options.length <= 1) {
            for (const c of await api("/api/admin/camioneros")) {
                const o = document.createElement("option");
                o.value = c.id;
                o.textContent = c.nombre;
                sel.appendChild(o);
            }
        }
        const p = sel.value ? "?camioneroId=" + sel.value : "";
        const lista = await api("/api/admin/accesos" + p);
        $("#l-accesos").innerHTML = lista.length
            ? `<table>
                 <thead><tr><th>Fecha</th><th>Camionero</th><th>Evento</th><th>IP</th><th>Dispositivo</th></tr></thead>
                 <tbody>${lista.map((a) => `
                   <tr>
                     <td>${fmt(a.creadoAt)}</td>
                     <td>${esc(a.camionero)}</td>
                     <td>${esc(a.evento)}</td>
                     <td>${esc(a.ip)}</td>
                     <td title="${esc(a.dispositivo)}">${esc((a.dispositivo || "").slice(0, 60))}</td>
                   </tr>`).join("")}</tbody>
               </table>`
            : "<p>Sin registros.</p>";
    } catch (e) { $("#msg").textContent = e.message; }
}
$("#btn-accesos").onclick = cargarAccesos;
// ---------- al abrir: ¿ya hay sesión? ----------
fetch("/api/auth/me")
    .then((r) => r.ok ? r.json() : null)
    .then((d) => mostrar(!!d, d?.usuario))
    .catch(() => {
        mostrar(false);
        $("#msg").textContent = "No se pudo conectar con el servidor.";
    });