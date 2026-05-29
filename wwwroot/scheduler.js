// ========================
//  Global state
// ========================
let schedules = [];

// ========================
//  Format date for display
// ========================
function formatDate(dateString) {
    if (!dateString) return "-";
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return "-";
    return d.toLocaleString('es-ES', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// ========================
//  Load all schedules from API
// ========================
async function loadSchedules() {
    const loading = document.getElementById("loading");
    const tableContainer = document.getElementById("tableContainer");
    const errorMessage = document.getElementById("errorMessage");

    errorMessage.textContent = "";
    loading.classList.add("visible");
    tableContainer.style.display = "none";

    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules`);

        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        schedules = await res.json();
        renderSchedulesTable(schedules);
        tableContainer.style.display = "block";
    } catch (error) {
        errorMessage.textContent = `Error cargando programaciones: ${error.message}`;
    } finally {
        loading.classList.remove("visible");
    }
}

// ========================
//  Render schedules table
// ========================
function renderSchedulesTable(data) {
    const tbody = document.getElementById("schedulesBody");
    tbody.innerHTML = "";

    if (!data || data.length === 0) {
        tbody.innerHTML = "<tr><td colspan='7' style='text-align:center;'>Sin programaciones configuradas</td></tr>";
        return;
    }

    data.forEach(schedule => {
        const tr = document.createElement("tr");

        // Cod Envio
        const codEnvioCell = document.createElement("td");
        codEnvioCell.textContent = schedule.codEnvio || "-";
        tr.appendChild(codEnvioCell);

        // Tipo Entidad
        const tipoEntidadCell = document.createElement("td");
        tipoEntidadCell.textContent = schedule.tipoEntidad || "-";
        tr.appendChild(tipoEntidadCell);

        // Codigo
        const codigoCell = document.createElement("td");
        codigoCell.textContent = schedule.codigo || "-";
        tr.appendChild(codigoCell);

        // Cron Expression
        const cronCell = document.createElement("td");
        cronCell.textContent = schedule.cronExpression || "-";
        cronCell.style.fontFamily = "monospace";
        tr.appendChild(cronCell);

        // IsActive status badge
        const statusCell = document.createElement("td");
        const statusBadge = document.createElement("span");
        statusBadge.className = "status-badge";
        statusBadge.textContent = schedule.isActive ? "Activo" : "Inactivo";
        statusBadge.classList.add(schedule.isActive ? "status-active" : "status-inactive");
        statusCell.appendChild(statusBadge);
        tr.appendChild(statusCell);

        // CreatedAt
        const createdCell = document.createElement("td");
        createdCell.textContent = formatDate(schedule.createdAt);
        tr.appendChild(createdCell);

        // Actions
        const actionsCell = document.createElement("td");
        const actionWrapper = document.createElement("div");
        actionWrapper.className = "action-buttons";

        // Edit button
        const editBtn = document.createElement("button");
        editBtn.className = "btn btn-primary";
        editBtn.textContent = "Editar";
        editBtn.onclick = () => openEditModal(schedule);
        actionWrapper.appendChild(editBtn);

        // Toggle active/inactive button
        const toggleBtn = document.createElement("button");
        toggleBtn.className = "btn btn-secondary";
        toggleBtn.textContent = schedule.isActive ? "Desactivar" : "Activar";
        toggleBtn.onclick = () => toggleSchedule(schedule.id);
        actionWrapper.appendChild(toggleBtn);

        // Delete button
        const deleteBtn = document.createElement("button");
        deleteBtn.className = "btn btn-danger";
        deleteBtn.textContent = "Eliminar";
        deleteBtn.onclick = () => deleteSchedule(schedule.id, schedule.codEnvio);
        actionWrapper.appendChild(deleteBtn);

        actionsCell.appendChild(actionWrapper);
        tr.appendChild(actionsCell);
        tbody.appendChild(tr);
    });
}

// ========================
//  Open create modal
// ========================
function openCreateModal() {
    document.getElementById("modalTitle").textContent = "Nueva Programación";
    document.getElementById("scheduleId").value = "";
    document.getElementById("codEnvio").value = "";
    document.getElementById("tipoEntidad").value = "";
    document.getElementById("codigo").value = "";
    document.getElementById("cronExpression").value = "";
    document.getElementById("isActive").checked = true;
    document.getElementById("isActiveGroup").style.display = "none";
    document.getElementById("scheduleModal").style.display = "flex";
}

// ========================
//  Open edit modal with existing schedule data
// ========================
function openEditModal(schedule) {
    document.getElementById("modalTitle").textContent = "Editar Programación";
    document.getElementById("scheduleId").value = schedule.id;
    document.getElementById("codEnvio").value = schedule.codEnvio;
    document.getElementById("tipoEntidad").value = schedule.tipoEntidad;
    document.getElementById("codigo").value = schedule.codigo;
    document.getElementById("cronExpression").value = schedule.cronExpression;
    document.getElementById("isActive").checked = schedule.isActive;
    document.getElementById("isActiveGroup").style.display = "block";
    document.getElementById("scheduleModal").style.display = "flex";
}

// ========================
//  Close modal
// ========================
function closeModal() {
    document.getElementById("scheduleModal").style.display = "none";
    document.getElementById("scheduleForm").reset();
}

// ========================
//  Save schedule (create or update)
// ========================
async function saveSchedule(event) {
    event.preventDefault();

    const id = document.getElementById("scheduleId").value;
    const codEnvio = document.getElementById("codEnvio").value.trim();
    const tipoEntidad = document.getElementById("tipoEntidad").value.trim();
    const codigo = document.getElementById("codigo").value.trim();
    const cronExpression = document.getElementById("cronExpression").value.trim();
    const isActive = document.getElementById("isActive").checked;

    const saveBtn = document.getElementById("saveBtn");
    saveBtn.disabled = true;
    saveBtn.textContent = "Guardando...";

    try {
        let res;
        if (id) {
            // Update existing schedule
            res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    codEnvio,
                    tipoEntidad,
                    codigo,
                    cronExpression,
                    isActive
                })
            });
        } else {
            // Create new schedule
            res = await authenticatedFetch(`${API_BASE}/api/schedules`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    codEnvio,
                    tipoEntidad,
                    codigo,
                    cronExpression
                })
            });
        }

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        showToast(id ? "Programación actualizada correctamente" : "Programación creada correctamente", "success");
        closeModal();
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    } finally {
        saveBtn.disabled = false;
        saveBtn.textContent = "Guardar";
    }
}

// ========================
//  Toggle schedule active/inactive state
// ========================
async function toggleSchedule(id) {
    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}/toggle`, {
            method: "PATCH"
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        const updated = await res.json();
        showToast(`Programación ${updated.isActive ? "activada" : "desactivada"} correctamente`, "success");
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    }
}

// ========================
//  Delete schedule
// ========================
async function deleteSchedule(id, codEnvio) {
    if (!confirm(`¿Está seguro de eliminar la programación "${codEnvio}"?`)) {
        return;
    }

    try {
        const res = await authenticatedFetch(`${API_BASE}/api/schedules/${id}`, {
            method: "DELETE"
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `HTTP ${res.status}`);
        }

        showToast("Programación eliminada correctamente", "success");
        await loadSchedules();
    } catch (error) {
        showToast(`Error: ${error.message}`, "error");
    }
}

// ========================
//  Toast notification
// ========================
function showToast(message, type) {
    const toast = document.getElementById("toast");
    toast.textContent = message;
    toast.className = "toast visible";

    if (type === "success") {
        toast.classList.add("toast-success");
    } else {
        toast.classList.add("toast-error");
    }

    setTimeout(() => {
        toast.classList.remove("visible");
    }, 4000);
}

// ========================
//  Close modal on overlay click
// ========================
document.getElementById("scheduleModal").addEventListener("click", function (e) {
    if (e.target === this) {
        closeModal();
    }
});

// ========================
//  Close modal on Escape key
// ========================
document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
        closeModal();
    }
});

// ========================
//  On page load: check if authenticated
// ========================
(async function init() {
    const authenticated = await ensureAuthenticated();
    if (authenticated) {
        await loadSchedules();
        scheduleAutoRefresh();
    } else {
        clearTokens();
        window.location.href = "login.html";
    }
})();
