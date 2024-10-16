using System.Windows;

namespace DAQSystem.Application.Themes
{
    /// <summary>
    /// A DataGrid text column using default Modern UI element styles.
    /// </summary>
    public class DataGridTextColumn
        : System.Windows.Controls.DataGridTextColumn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridTextColumn"/> class.
        /// </summary>
        public DataGridTextColumn()
        {
            this.ElementStyle = System.Windows.Application.Current.Resources["DataGridTextStyle"] as Style;
            this.EditingElementStyle = System.Windows.Application.Current.Resources["DataGridEditingTextStyle"] as Style;
        }
    }
}
