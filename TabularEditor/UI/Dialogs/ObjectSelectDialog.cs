using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TabularEditor.PropertyGridUI;
using TabularEditor.TOMWrapper;

namespace TabularEditor.UI.Dialogs
{
    public partial class ObjectSelectDialog<T> : Form where T: TabularNamedObject
    {
        string[] _objectNames;
        string[] _displayNames;
        List<T> _objects;
        List<T> _allObjects;
        HashSet<T> _selectedObjects = new HashSet<T>();
        public T SelectedObject { get; set; }
        public object[] SelectedObjects { get; set; }
        private readonly bool _multiSelect;
        private readonly bool _allowNoSelection;
        private string _searchBuffer = string.Empty;
        private DateTime _lastSearchKeyPress = DateTime.MinValue;
        private Func<T, string> _displayNameSelector = o => o.Name;
        private Func<T, bool> _highlightPredicate = o => false;
        private bool _rebuildingListView;

        public ObjectSelectDialog(bool multiSelect, bool allowNoSelection)
        {
            InitializeComponent();

            listView1.Resize += ListView1_Resize;
            listView1.KeyPress += ListView1_KeyPress;
            listView1.KeyDown += ListView1_KeyDown;
            listView1.MultiSelect = multiSelect;
            btnClear.Click += (s, e) => ClearSelection();
            _multiSelect = multiSelect;
            _allowNoSelection = allowNoSelection;
            btnClear.Visible = _allowNoSelection || multiSelect;
        }

        private void ClearSelection()
        {
            if (_multiSelect) _selectedObjects.Clear();
            listView1.SelectedItems.Clear();
            listView1_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private bool suspendResize = false;

        private void ListView1_Resize(object sender, EventArgs e)
        {
            if (suspendResize) return;
            suspendResize = true;
            columnHeader1.Width = listView1.ClientRectangle.Width - 2;
            suspendResize = false;
        }

        public void Setup(IEnumerable<T> columns, Func<T, string> displayNameSelector = null, Func<T, bool> highlightPredicate = null, bool preserveOrder = false)
        {
            _allObjects = preserveOrder ? columns.ToList() : columns.OrderBy(c => c.Name).ToList();
            _displayNameSelector = displayNameSelector ?? (o => o.Name);
            _highlightPredicate = highlightPredicate ?? (o => false);
            _selectedObjects.Clear();
            _searchBuffer = string.Empty;
            RebuildListView();
        }

        private void RebuildListView()
        {
            var selected = _multiSelect
                ? _selectedObjects
                : new HashSet<T>(listView1.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as T).Where(o => o != null));

            var filtered = string.IsNullOrWhiteSpace(_searchBuffer)
                ? _allObjects
                : _allObjects.Where(c => _displayNameSelector(c).IndexOf(_searchBuffer, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _rebuildingListView = true;
            listView1.Items.Clear();
            this._objects = filtered;
            this._objectNames = this._objects.Select(c => c.Name).ToArray();
            this._displayNames = this._objects.Select(c => _displayNameSelector(c)).ToArray();

            var items = this._objects.Select((c, i) =>
            {
                var item = new ListViewItem(_displayNames[i]) { Tag = c };
                if (_highlightPredicate(c))
                {
                    item.BackColor = SystemColors.Info;
                }
                return item;
            }).ToArray();
            listView1.Items.AddRange(items);

            foreach (ListViewItem item in listView1.Items)
            {
                if (selected.Contains(item.Tag as T)) item.Selected = true;
            }
            _rebuildingListView = false;

            listView1_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private void ListView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back && _searchBuffer.Length > 0)
            {
                _searchBuffer = _searchBuffer.Substring(0, _searchBuffer.Length - 1);
                RebuildListView();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && _searchBuffer.Length > 0)
            {
                _searchBuffer = string.Empty;
                RebuildListView();
                e.Handled = true;
            }
        }

        private void ListView1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            if ((DateTime.Now - _lastSearchKeyPress).TotalSeconds > 1) _searchBuffer = string.Empty;
            _lastSearchKeyPress = DateTime.Now;
            _searchBuffer += e.KeyChar;
            RebuildListView();

            if (!_multiSelect && listView1.Items.Count > 0 && listView1.SelectedItems.Count == 0)
            {
                listView1.Items[0].Selected = true;
                listView1.Items[0].EnsureVisible();
            }
            e.Handled = true;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_rebuildingListView) return;

            if (_multiSelect)
            {
                SelectedObject = null;
                foreach (ListViewItem item in listView1.Items)
                {
                    var obj = item.Tag as T;
                    if (obj == null) continue;

                    if (item.Selected) _selectedObjects.Add(obj);
                    else _selectedObjects.Remove(obj);
                }

                if (_selectedObjects.Count > 0)
                {
                    btnOK.Enabled = true;
                    SelectedObjects = _selectedObjects.Cast<object>().ToArray();
                }
                else
                {
                    if (!_allowNoSelection) btnOK.Enabled = false;
                    SelectedObjects = Array.Empty<object>();
                }

            }
            else
            {
                SelectedObjects = null;
                if (listView1.SelectedIndices.Count == 1)
                {
                    btnOK.Enabled = true;
                    SelectedObject = listView1.SelectedItems[0].Tag as T;
                }
                else
                {
                    if(!_allowNoSelection) btnOK.Enabled = false;
                    SelectedObject = null;
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 1)
                listView1.EnsureVisible(listView1.SelectedIndices[0]);
            listView1.Focus();
            base.OnShown(e);
        }

        protected void Prep(IEnumerable<T> columns, IEnumerable<T> preselectedColumns, string label)
        {
            label1.Text = label;
            Setup(columns);
            listView1.SelectedIndices.Clear();
            var preselected = preselectedColumns?.ToList() ?? new List<T>();
            _selectedObjects = new HashSet<T>(preselected);
            btnOK.Enabled = _allowNoSelection || _selectedObjects.Any();
            SelectedObjects = _selectedObjects.Cast<object>().ToArray();
            foreach (var preselect in preselected)
            {
                var ix = this._objects.IndexOf(preselect);
                if (ix >= 0)
                {
                    listView1.SelectedIndices.Add(ix);
                    listView1.EnsureVisible(ix);
                }
            }
        }

        protected void Prep(IEnumerable<T> columns, T preselect, string label)
        {
            label1.Text = label;
            Setup(columns);
            listView1.SelectedIndices.Clear();
            var ix = this._objects.IndexOf(preselect);
            if (ix >= 0)
            {
                listView1.SelectedIndices.Add(ix);
                listView1.EnsureVisible(ix);
                btnOK.Enabled = true;
            }
            else
            {
                btnOK.Enabled = false;
            }
        }

        protected void Prep(IEnumerable<T> columns, T preselect, string label, Func<T, string> displayNameSelector, Func<T, bool> highlightPredicate, Func<T, bool> autoSelectPredicate = null)
        {
            label1.Text = label;
            Setup(columns, displayNameSelector, highlightPredicate, preserveOrder: true);
            listView1.SelectedIndices.Clear();

            var ix = this._objects.IndexOf(preselect);
            if (ix >= 0)
            {
                listView1.SelectedIndices.Add(ix);
                listView1.EnsureVisible(ix);
                btnOK.Enabled = true;
                return;
            }

            if (autoSelectPredicate != null)
            {
                var autoSelectIx = this._objects.FindIndex(o => autoSelectPredicate(o));
                if (autoSelectIx >= 0)
                {
                    listView1.SelectedIndices.Add(autoSelectIx);
                    listView1.EnsureVisible(autoSelectIx);
                    btnOK.Enabled = true;
                    return;
                }
            }

            btnOK.Enabled = _allowNoSelection;
        }

        public static T SelectObject(IEnumerable<T> columns, T preselect = null, string label = "Select object:")
        {
            var selector = new ObjectSelectDialog<T>(multiSelect: false, allowNoSelection: true);
            selector.Text = "Select " + typeof(T).Name;
            selector.Prep(columns, preselect, label);
            if (selector.ShowDialog() == DialogResult.Cancel) return null;
            else return selector.SelectedObject;
        }
    }

    public class ColumnSelectDialog: ObjectSelectDialog<Column>, ICustomEditor
    {
        public ColumnSelectDialog(bool multiSelect, bool allowNoSelection): base(multiSelect, allowNoSelection)
        {

        }

        public object Edit(object instance, string property, object value, out bool cancel)
        {
            var instanceColumn = instance as Column ?? (instance as object[])?.First() as Column;
            var tuca = instance as TimeUnitColumnAssociation;

            switch (property)
            {
                case nameof(Column.SortByColumn):
                    Prep(instanceColumn.Table.Columns.Where(c => c != instanceColumn), value as Column, "Select column:");
                    break;
                case nameof(Column.GroupByColumns):
                    Prep(instanceColumn.Table.Columns.Where(c => c != instanceColumn), (value as IEnumerable<object>)?.Cast<Column>(), "Select column:");
                    break;
                case nameof(TimeRelatedColumnGroup.Columns):
                    var trcg = instance as TimeRelatedColumnGroup;
                    Prep(trcg.Calendar.Table.Columns, (value as IEnumerable<object>)?.Cast<Column>(), "Select columns:");
                    break;
                case nameof(TimeUnitColumnAssociation.AssociatedColumns):
                    Prep(tuca.Calendar.Table.Columns, (value as IEnumerable<object>)?.Cast<Column>(), "Select columns:");
                    break;
                case nameof(TimeUnitColumnAssociation.PrimaryColumn):
                    Prep(tuca.Calendar.Table.Columns, tuca.PrimaryColumn, "Select column:");
                    break;
            }

            if (ShowDialog() == DialogResult.Cancel)
            {
                cancel = true;
                return null;
            }

            cancel = false;
            return SelectedObject ?? (object)SelectedObjects;
        }
    }

    public class RelationshipColumnSelectDialog : ObjectSelectDialog<Column>, ICustomEditor
    {
        public RelationshipColumnSelectDialog() : base(multiSelect: false, allowNoSelection: true)
        {
        }

        public object Edit(object instance, string property, object value, out bool cancel)
        {
            var relationship = instance as SingleColumnRelationship ?? (instance as object[])?.FirstOrDefault() as SingleColumnRelationship;
            if (relationship == null)
            {
                cancel = true;
                return value;
            }

            var model = relationship.Model;
            var fromColumn = relationship.FromColumn;
            var toColumn = relationship.ToColumn;
            var selectedValue = value as Column;

            var prioritizeByFromName = property == nameof(SingleColumnRelationship.ToColumn)
                && toColumn == null
                && fromColumn != null;

            Func<Column, bool> highlightPredicate = c => prioritizeByFromName && c != fromColumn && c.Name == fromColumn.Name;

            var allColumns = model.Tables.SelectMany(t => t.Columns);
            if (property == nameof(SingleColumnRelationship.ToColumn) && fromColumn != null)
            {
                allColumns = allColumns.Where(c => c != fromColumn);
            }
            else if (property == nameof(SingleColumnRelationship.FromColumn) && toColumn != null)
            {
                allColumns = allColumns.Where(c => c != toColumn);
            }

            if (prioritizeByFromName)
            {
                allColumns = allColumns
                    .OrderByDescending(c => c != fromColumn && c.Name == fromColumn.Name)
                    .ThenBy(c => c.DaxObjectFullName);
            }
            else
            {
                allColumns = allColumns.OrderBy(c => c.DaxObjectFullName);
            }

            Prep(
                allColumns,
                selectedValue,
                "Select column:",
                c => c.DaxObjectFullName,
                highlightPredicate,
                autoSelectPredicate: highlightPredicate);

            if (ShowDialog() == DialogResult.Cancel)
            {
                cancel = true;
                return value;
            }

            cancel = false;
            return SelectedObject;
        }
    }
}
