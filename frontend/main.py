from __future__ import annotations

import os
from typing import Any

import flet as ft
import httpx


API_BASE_URL = os.getenv("ARAPI_URL", "http://localhost:5030").rstrip("/")


class DeputadosView:
    def __init__(self, page: ft.Page) -> None:
        self.page = page
        self.deputados: list[dict[str, Any]] = []
        self.current_page = 1
        self.page_size = 100
        self.total_results = 0
        self.total_pages = 1

        self.search = ft.TextField(
            label="Nome do deputado",
            hint_text="Pesquisar por nome parlamentar ou completo",
            prefix_icon=ft.Icons.SEARCH,
            expand=True,
            on_submit=self.search_deputados,
        )
        self.group = ft.TextField(label="Grupo", width=150)
        self.legislatura = ft.TextField(label="Legislatura", width=150)
        self.status = ft.Dropdown(
            label="Situação",
            width=155,
            value="",
            options=[
                ft.DropdownOption(key="", text="Todas"),
                ft.DropdownOption(key="Ativo", text="Ativo"),
                ft.DropdownOption(key="Inativo", text="Inativo"),
            ],
        )
        self.feedback = ft.Text(size=13)
        self.result_count = ft.Text("0 resultados", color="#64748B")
        self.progress = ft.ProgressBar(visible=False, color="#0F766E")
        self.page_label = ft.Text("Página 1 de 1", color="#64748B")
        self.previous_button = ft.IconButton(
            icon=ft.Icons.ARROW_BACK,
            tooltip="Página anterior",
            disabled=True,
            on_click=self.previous_page,
        )
        self.next_button = ft.IconButton(
            icon=ft.Icons.ARROW_FORWARD,
            tooltip="Página seguinte",
            disabled=True,
            on_click=self.next_page,
        )
        self.table = ft.DataTable(
            column_spacing=24,
            heading_row_color="#E2E8F0",
            data_row_min_height=62,
            columns=[
                ft.DataColumn(ft.Text("Deputado", weight=ft.FontWeight.BOLD)),
                ft.DataColumn(ft.Text("Grupo", weight=ft.FontWeight.BOLD)),
                ft.DataColumn(ft.Text("Círculo", weight=ft.FontWeight.BOLD)),
                ft.DataColumn(ft.Text("Situação", weight=ft.FontWeight.BOLD)),
                ft.DataColumn(ft.Text("Detalhes", weight=ft.FontWeight.BOLD)),
            ],
            rows=[],
        )
        self.empty_state = ft.Container(
            content=ft.Column(
                [
                    ft.Icon(ft.Icons.INBOX_OUTLINED, size=42, color="#94A3B8"),
                    ft.Text("Nenhum deputado encontrado", color="#64748B"),
                ],
                horizontal_alignment=ft.CrossAxisAlignment.CENTER,
                spacing=8,
            ),
            alignment=ft.Alignment(0, 0),
            padding=45,
            visible=True,
        )

    def start(self) -> None:
        self.page.title = "ARAPI | Deputados"
        self.page.theme_mode = ft.ThemeMode.LIGHT
        self.page.bgcolor = "#F1F5F9"
        self.page.padding = 0
        self.page.window_min_width = 900
        self.page.window_min_height = 620

        sidebar = ft.Container(
            width=235,
            bgcolor="#123B4A",
            padding=26,
            content=ft.Column(
                [
                    ft.Text("ARAPI", size=26, weight=ft.FontWeight.BOLD, color="#FFFFFF"),
                    ft.Text("Painel parlamentar", size=12, color="#A7D7D1"),
                    ft.Divider(color="#35626B", height=34),
                    ft.Container(
                        content=ft.Row(
                            [ft.Icon(ft.Icons.GROUP_OUTLINED, color="#FFFFFF"), ft.Text("Deputados", color="#FFFFFF")],
                            spacing=12,
                        ),
                        bgcolor="#1E5963",
                        border_radius=8,
                        padding=12,
                    ),
                    ft.Text("Fonte: OpenAR", size=12, color="#A7D7D1"),
                ],
                spacing=14,
            ),
        )

        header = ft.Row(
            [
                ft.Column(
                    [
                        ft.Text("Deputados", size=30, weight=ft.FontWeight.BOLD, color="#123B4A"),
                        ft.Text("Consulte e sincronize os dados parlamentares.", color="#64748B"),
                    ],
                    expand=True,
                    spacing=4,
                ),
                ft.FilledButton("Sincronizar base", icon=ft.Icons.SYNC, on_click=self.sync_database),
            ],
            vertical_alignment=ft.CrossAxisAlignment.CENTER,
        )

        filters = ft.Container(
            bgcolor="#FFFFFF",
            border_radius=10,
            padding=18,
            content=ft.Column(
                [
                    ft.Row([self.search, self.group, self.legislatura, self.status], spacing=10),
                    ft.Row(
                        [
                            ft.FilledButton("Pesquisar", icon=ft.Icons.SEARCH, on_click=self.search_deputados),
                            ft.TextButton("Limpar", icon=ft.Icons.CLEAR, on_click=self.clear_filters),
                        ],
                        spacing=8,
                    ),
                ],
                spacing=12,
            ),
        )

        results = ft.Container(
            expand=True,
            bgcolor="#FFFFFF",
            border_radius=10,
            padding=10,
            content=ft.Column(
                [
                    ft.Row(
                        [ft.Text("Resultados", size=18, weight=ft.FontWeight.BOLD), self.result_count],
                        spacing=12,
                    ),
                    ft.Row(
                        [self.previous_button, self.page_label, self.next_button],
                        alignment=ft.MainAxisAlignment.CENTER,
                        spacing=8,
                    ),
                    self.progress,
                    self.feedback,
                    ft.Column(
                        [
                            ft.Row([self.table], scroll=ft.ScrollMode.AUTO),
                            self.empty_state,
                        ],
                        expand=True,
                        scroll=ft.ScrollMode.AUTO,
                    ),
                ],
                expand=True,
                spacing=12,
            ),
        )

        content = ft.Container(
            expand=True,
            padding=30,
            content=ft.Column([header, filters, results], expand=True, spacing=18),
        )
        self.page.add(ft.Row([sidebar, content], expand=True, spacing=0))
        self.page.run_task(self.load_deputados)

    def query_params(self) -> dict[str, str]:
        values = {
            "q": self.search.value,
            "grupo": self.group.value,
            "legislatura": self.legislatura.value,
            "situacao": self.status.value,
            "page": str(self.current_page),
            "limit": str(self.page_size),
        }
        return {key: value.strip() for key, value in values.items() if value and value.strip()}

    async def load_deputados(self, _event: Any = None) -> None:
        self.set_loading(True)
        try:
            async with httpx.AsyncClient(timeout=30) as client:
                response = await client.get(f"{API_BASE_URL}/api/deputados/consultar", params=self.query_params())
                response.raise_for_status()
                payload = response.json()

            if isinstance(payload, dict):
                self.deputados = payload.get("data", [])
                self.total_results = int(payload.get("total", len(self.deputados)))
                self.current_page = int(payload.get("page", self.current_page))
                self.page_size = int(payload.get("limit", self.page_size))
            else:
                self.deputados = payload
                self.total_results = len(self.deputados)

            self.total_pages = max(1, (self.total_results + self.page_size - 1) // self.page_size)
            self.feedback.value = ""
            self.render_table()
        except httpx.HTTPError as error:
            self.deputados = []
            self.feedback.value = f"Não foi possível consultar a API: {error}"
            self.render_table()
        finally:
            self.set_loading(False)

    def search_deputados(self, _event: Any = None) -> None:
        self.current_page = 1
        self.page.run_task(self.load_deputados)

    async def sync_database(self, _event: Any = None) -> None:
        self.set_loading(True)
        try:
            async with httpx.AsyncClient(timeout=120) as client:
                response = await client.post(f"{API_BASE_URL}/api/deputados/sincronizar")
                response.raise_for_status()
                result = response.json()
            total = result.get("total", 0) if isinstance(result, dict) else 0
            self.feedback.value = f"Sincronização concluída: {total} deputado(s) gravado(s)."
            await self.load_deputados()
        except httpx.HTTPError as error:
            self.feedback.value = f"Falha na sincronização: {error}"
        finally:
            self.set_loading(False)

    def render_table(self) -> None:
        self.result_count.value = f"{self.total_results} resultado(s)"
        self.page_label.value = f"Página {self.current_page} de {self.total_pages}"
        self.previous_button.disabled = self.current_page <= 1
        self.next_button.disabled = self.current_page >= self.total_pages
        self.empty_state.visible = not self.deputados
        self.table.rows = [
            ft.DataRow(
                cells=[
                    ft.DataCell(
                        ft.Column(
                            [
                                ft.Text(str(item.get("nomeParlamentar") or "Sem nome"), weight=ft.FontWeight.BOLD),
                                ft.Text(str(item.get("nomeCompleto") or ""), size=12, color="#64748B"),
                            ],
                            spacing=2,
                        )
                    ),
                    ft.DataCell(ft.Text(str(item.get("grupoParlamentar") or "-"))),
                    ft.DataCell(ft.Text(str(item.get("circuloEleitoral") or "-"))),
                    ft.DataCell(self.status_badge(str(item.get("situacao") or "-"))),
                    ft.DataCell(
                        ft.IconButton(
                            icon=ft.Icons.VISIBILITY_OUTLINED,
                            tooltip="Ver detalhes",
                            on_click=lambda _event, selected=item: self.show_details(selected),
                        )
                    ),
                ]
            )
            for item in self.deputados
        ]
        self.page.update()

    def show_details(self, deputy: dict[str, Any]) -> None:
        rows = [
            ("Nome parlamentar", deputy.get("nomeParlamentar")),
            ("Nome completo", deputy.get("nomeCompleto")),
            ("Grupo parlamentar", deputy.get("grupoParlamentar")),
            ("Círculo eleitoral", deputy.get("circuloEleitoral")),
            ("Legislatura", deputy.get("legislaturaId")),
            ("Situação", deputy.get("situacao")),
            ("ID", deputy.get("id")),
        ]
        dialog = ft.AlertDialog(
            modal=True,
            title=ft.Text("Detalhes do deputado"),
            content=ft.Column(
                [ft.Row([ft.Text(label, weight=ft.FontWeight.BOLD, width=155), ft.Text(str(value or "-"), expand=True)]) for label, value in rows],
                tight=True,
                width=520,
                spacing=12,
            ),
            actions=[ft.TextButton("Fechar", on_click=lambda _event: self.close_dialog(dialog))],
        )
        self.page.dialog = dialog
        dialog.open = True
        self.page.update()

    def close_dialog(self, dialog: ft.AlertDialog) -> None:
        dialog.open = False
        self.page.update()

    @staticmethod
    def status_badge(value: str) -> ft.Control:
        active = value.lower() in {"ativo", "active"}
        return ft.Container(
            content=ft.Text(value, size=12, color="#047857" if active else "#92400E"),
            bgcolor="#D1FAE5" if active else "#FEF3C7",
            border_radius=18,
            padding=ft.Padding(left=10, top=6, right=10, bottom=6),
        )

    def clear_filters(self, _event: Any = None) -> None:
        self.search.value = ""
        self.group.value = ""
        self.legislatura.value = ""
        self.status.value = ""
        self.current_page = 1
        self.page.run_task(self.load_deputados)

    def previous_page(self, _event: Any = None) -> None:
        if self.current_page > 1:
            self.current_page -= 1
            self.page.run_task(self.load_deputados)

    def next_page(self, _event: Any = None) -> None:
        if self.current_page < self.total_pages:
            self.current_page += 1
            self.page.run_task(self.load_deputados)

    def set_loading(self, loading: bool) -> None:
        self.progress.visible = loading
        self.page.update()


def main(page: ft.Page) -> None:
    DeputadosView(page).start()


if __name__ == "__main__":
    ft.app(target=main)
