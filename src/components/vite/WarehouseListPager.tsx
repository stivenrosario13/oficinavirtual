import './warehouseListPager.css';
export default function WarehouseListPager({page, pages, total, start, end, onPage}: {page: number; pages: number; total: number; start: number; end: number; onPage: (page: number) => void}) {
  return <nav className="warehouse-list-pager" aria-label="Páginas del listado">
    <span>{start}–{end} de {total} registros</span>
    <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>Anterior</button>
    <strong>Página {page} de {pages}</strong>
    <button type="button" disabled={page >= pages} onClick={() => onPage(page + 1)}>Siguiente</button>
  </nav>;
}
