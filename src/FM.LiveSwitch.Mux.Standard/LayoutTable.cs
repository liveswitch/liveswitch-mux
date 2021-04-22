namespace FM.LiveSwitch.Mux
{
    public class LayoutTable
    {
        public int ColumnCount { get; set; }
        public int RowCount { get; set; }
        public Size CellSize { get; set; }

        public LayoutTable(int columnCount, int rowCount, Size cellSize)
        {
            ColumnCount = columnCount;
            RowCount = rowCount;
            CellSize = cellSize;
        }
    }
}