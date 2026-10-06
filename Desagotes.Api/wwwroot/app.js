const $ = (s) => document.querySelector(s);
const esc = (s) => String(s ?? "").replace(/[&<>"]/g,
    (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const fotos = {};   // fotos sacadas, por clave: checkin, checkout, remito, camara

// ---------- utilidades ----------
function mensaje(texto, error = false) {
    const m = $("#msg");
    m.textContent = texto;
    m.className = error ? "error" : "ok";
}

async function api(url, opciones) {
    const r = await fetch(url, opciones);
    const datos = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(datos.mensaje || datos.title || "Error " + r.status);
    return datos;
}

// Deshabilita el botón mientras se envía (evita el doble toque)
async function enviar(boton, tarea) {
    boton.disabled = true;
    try { await tarea(); }
    catch (e) { mensaje(e.message, true); }
    finally { boton.disabled = false; }
}

const camioneroId = () => $("#camionero").value;

// ---------- cámara en vivo ----------
async function capturar() {
    // Abre el selector de archivos (en el celular: cámara o galería)
    

   
    let stream;
    try {
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "environment", width: { ideal: 1600 } },
                audio: false
            });
        } catch (e1) {
            // Reintento sin preferencias (PC con webcam común)
            stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        }
    } catch (e) {
        throw new Error(`No se pudo abrir la cámara (${e.name}). Revisá el permiso del navegador.`);
    }

    const video = $("#video");
    video.srcObject = stream;
    $("#camara").hidden = false;

    return new Promise((resolve) => {
        const cerrar = () => {
            stream.getTracks().forEach((t) => t.stop());
            $("#camara").hidden = true;
        };
        $("#cam-cancelar").onclick = () => { cerrar(); resolve(null); };
        $("#cam-disparar").onclick = () => {
            const escala = Math.min(1, 1600 / video.videoWidth);
            const c = document.createElement("canvas");
            c.width = video.videoWidth * escala;
            c.height = video.videoHeight * escala;
            c.getContext("2d").drawImage(video, 0, 0, c.width, c.height);
            c.toBlob((blob) => { cerrar(); resolve(blob); }, "image/jpeg", 0.8);
        };
    });
}

function elegirArchivo() {
    return new Promise((resolve) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = "image/*";
        input.onchange = () => resolve(input.files[0] ?? null);
        input.oncancel = () => resolve(null);
        input.click();
    });
}

// Achica la imagen a 1600 px y la pasa a JPEG, igual que la cámara en vivo
async function reducir(archivo) {
    const url = URL.createObjectURL(archivo);
    try {
        const img = await new Promise((ok, mal) => {
            const i = new Image();
            i.onload = () => ok(i);
            i.onerror = () => mal(new Error("No se pudo leer la imagen."));
            i.src = url;
        });
        const escala = Math.min(1, 1600 / Math.max(img.naturalWidth, img.naturalHeight));
        const c = document.createElement("canvas");
        c.width = img.naturalWidth * escala;
        c.height = img.naturalHeight * escala;
        c.getContext("2d").drawImage(img, 0, 0, c.width, c.height);
        return await new Promise((res) => c.toBlob(res, "image/jpeg", 0.8));
    } finally {
        URL.revokeObjectURL(url);
    }
}

document.querySelectorAll(".foto").forEach((div) => {
    const clave = div.dataset.foto;
    const img = div.querySelector("img");
    div.querySelector("button").onclick = async () => {
        try {
            let blob;
            if (div.dataset.origen === "galeria") {
                const archivo = await elegirArchivo();
                blob = archivo ? await reducir(archivo) : null;
            } else {
                blob = await capturar();
            }
            if (!blob) return;
            fotos[clave] = blob;
            img.src = URL.createObjectURL(blob);
        } catch (e) { mensaje(e.message, true); }
    };
});

function limpiarFoto(clave) {
    delete fotos[clave];
    document.querySelector(`[data-foto="${clave}"] img`).removeAttribute("src");
}

// ---------- ubicación ----------
function ubicacion() {
    return new Promise((ok, mal) => {
        navigator.geolocation.getCurrentPosition(
            (p) => ok({ lat: p.coords.latitude, lng: p.coords.longitude, precision: p.coords.accuracy }),
            () => mal(new Error("No se pudo obtener la ubicación. Activá el GPS y dale permiso al navegador.")),
            { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 }
        );
    });
}

// ---------- pestañas ----------
document.querySelectorAll("nav button").forEach((b) => {
    b.onclick = () => {
        document.querySelectorAll("nav button").forEach((x) => x.classList.toggle("activa", x === b));
        document.querySelectorAll("main > section").forEach((s) => s.hidden = s.id !== b.dataset.tab);
        $("#msg").textContent = "";
        if (b.dataset.tab === "historial") cargarHistorial();
    };
});

// ---------- check-in ----------
$("#btn-checkin").onclick = (e) => enviar(e.target, async () => {
    if (!camioneroId()) throw new Error("Elegí tu nombre arriba.");
    if (!$("#vehiculo").value) throw new Error("Elegí la patente.");
    if (!fotos.checkin) throw new Error("La foto es obligatoria.");

    mensaje("Obteniendo ubicación...");
    const u = await ubicacion();

    const f = new FormData();
    f.append("camioneroId", camioneroId());
    f.append("vehiculoId", $("#vehiculo").value);
    f.append("lat", u.lat);
    f.append("lng", u.lng);
    f.append("precisionM", u.precision);
    f.append("foto", fotos.checkin, "checkin.jpg");

    await api("/api/checkin", { method: "POST", body: f });
    mensaje("✅ Entrada registrada.");
    limpiarFoto("checkin");
});

// ---------- check-out ----------
$("#btn-checkout").onclick = (e) => enviar(e.target, async () => {
    if (!camioneroId()) throw new Error("Elegí tu nombre arriba.");
    const n = $("#declarados").value;
    if (n === "") throw new Error("Indicá cuántos remitos hiciste.");
    if (!fotos.checkout) throw new Error("La foto es obligatoria.");

    const f = new FormData();
    f.append("camioneroId", camioneroId());
    f.append("remitosDeclarados", n);
    f.append("foto", fotos.checkout, "checkout.jpg");

    const r = await api("/api/checkout", { method: "POST", body: f });
    mensaje(r.estado === "VALIDADA"
        ? "✅ Jornada cerrada y validada."
        : "✅ Salida registrada. Cargá los remitos desde el Historial.");
    limpiarFoto("checkout");
    $("#declarados").value = "";
});

// ---------- historial ----------
async function cargarHistorial() {
    const lista = $("#lista");
    if (!camioneroId()) { lista.innerHTML = "<p>Elegí tu nombre arriba.</p>"; return; }
    try {
        const js = await api("/api/jornadas?camioneroId=" + camioneroId());
        if (!js.length) { lista.innerHTML = "<p>Sin jornadas en los últimos 30 días.</p>"; return; }

        lista.innerHTML = js.map((j) => {
            const fecha = new Date(j.checkinAt).toLocaleDateString("es-AR");
            let estado, accion = "";
            if (j.estado === "ABIERTA") estado = "🟡 Abierta: falta el check-out";
            else if (j.estado === "VALIDADA") estado = "✅ Validada";
            else if (j.vencida) estado = `⚠️ Vencida: faltaron ${j.faltan} remito(s)`;
            else {
                const limite = new Date(j.limiteCarga).toLocaleString("es-AR");
                estado = `🟠 Faltan ${j.faltan} de ${j.declarados} remitos (hasta el ${limite})`;
                accion = `<button data-jornada="${j.id}">Cargar remito</button>`;
            }
            return `<div class="tarjeta"><strong>${fecha} · ${esc(j.patente)}</strong><p>${estado}</p>${accion}</div>`;
        }).join("");
    } catch (e) { mensaje(e.message, true); }
}

$("#lista").onclick = (e) => {
    const id = e.target.dataset.jornada;
    if (!id) return;
    $("#r-jornada").value = id;
    $("#remito-form").hidden = false;
    $("#remito-form").scrollIntoView();
};

$("#btn-remito").onclick = (e) => enviar(e.target, async () => {
    if (!$("#r-talonario").value || !$("#r-numero").value)
        throw new Error("Completá talonario y número de remito.");
    if (!fotos.remito || !fotos.camara)
        throw new Error("Hacen falta las dos fotos: remito y cámara desagotada.");

    const f = new FormData();
    f.append("jornadaId", $("#r-jornada").value);
    f.append("talonario", $("#r-talonario").value);
    f.append("nroRemito", $("#r-numero").value);
    f.append("cliente", $("#r-cliente").value);
    f.append("nroPedido", $("#r-pedido").value);
    f.append("fotoRemito", fotos.remito, "remito.jpg");
    f.append("fotoCamara", fotos.camara, "camara.jpg");

    const r = await api("/api/remitos", { method: "POST", body: f });
    mensaje(r.validada
        ? "✅ ¡Jornada validada!"
        : `✅ Remito guardado (${r.cargados} de ${r.declarados}).`);

    ["#r-talonario", "#r-numero", "#r-pedido"].forEach((s) => $(s).value = "");
    limpiarFoto("remito");
    limpiarFoto("camara");
    if (r.validada) $("#remito-form").hidden = true;
    cargarHistorial();
});

$("#btn-remito-cancelar").onclick = () => { $("#remito-form").hidden = true; };
function etiquetaPedido() {
    const telecom = $("#r-cliente").value === "TELECOM";
    $("#r-pedido-label").textContent = telecom
        ? "ID de obra (4 o 5 dígitos; si no lo tenés, dejalo vacío)"
        : "Pedido (6 dígitos; si no lo tenés, dejalo vacío)";
}
$("#r-cliente").onchange = etiquetaPedido;
etiquetaPedido();

// ---------- arranque ----------
async function llenar(select, url, campo) {
    const datos = await api(url);
    select.innerHTML = '<option value="">— elegir —</option>';
    for (const d of datos) {
        const o = document.createElement("option");
        o.value = d.id;
        o.textContent = d[campo];
        select.appendChild(o);
    }
}

$("#camionero").onchange = () => {
    try { localStorage.setItem("camionero", camioneroId()); } catch { }
    cargarHistorial();
};

(async function iniciar() {
    try {
        await llenar($("#camionero"), "/api/camioneros", "nombre");
        await llenar($("#vehiculo"), "/api/vehiculos", "patente");
        try { $("#camionero").value = localStorage.getItem("camionero") ?? ""; } catch { }
    } catch { mensaje("No se pudo conectar con el servidor.", true); }
})();