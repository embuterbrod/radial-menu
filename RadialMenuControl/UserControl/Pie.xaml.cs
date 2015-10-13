namespace RadialMenuControl.UserControl
{
    using Components;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// The Pie is the circle making a RadialMenuControl so radial - it generates PieSlices form RadialMenuButtons and manages interaction
    /// between those PieSlices, surfacing only useful information up to the parent RadialMenuControl. 
    /// </summary>
    public partial class Pie : UserControl, INotifyPropertyChanged
    {
        public readonly ObservableCollection<PieSlice> PieSlices = new ObservableCollection<PieSlice>();
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty SourceRadialMenuProperty =
            DependencyProperty.Register("SourceRadialMenu", typeof(RadialMenu), typeof(Pie), null);
        /// <summary>
        /// A Pie is generated by a RadialMenu - this is a reference to the RadialMenu that generated this pie.
        /// </summary>
        public RadialMenu SourceRadialMenu;

        private double _startAngle;
        /// <summary>
        /// Starting angle for the pie, with 0 being "north top". If set to 90, the first RadialMenuButton will be generated as
        /// as PieSlice on "east right".
        /// </summary>
        public double StartAngle
        {
            get { return _startAngle; }
            set {
                SetField(ref _startAngle, value);
                Draw();
            }
        }

        /// <summary>
        /// Angle of the Pie
        /// </summary>
        private double _angle;
        public double Angle
        {
            get { return _angle; }
            set { SetField(ref _angle, value); }
        }

        /// <summary>
        /// Size (aka diameter) of the pie
        /// </summary>
        private double _size;
        public double Size
        {
            get { return _size; }
            set
            {
                SetField(ref _size, value);

                Height = _size;
                Width = _size;
            }
        }

        private IList<RadialMenuButton> _slices = new List<RadialMenuButton>();
        /// <summary>
        /// A collection of RadialMenuButtons creating the slices on this pie
        /// </summary>
        public IList<RadialMenuButton> Slices
        {
            get { return _slices; }
            set
            {
                SetField(ref _slices, value);
                Draw();
            }
        }

        /// <summary>
        /// A shortcut to the label of the currently selected item (if an item is selected). 
        /// A selected item is the last selected radio button.
        /// </summary>
        public string SelectedItemValue => _selectedItem?.Label;

        private RadialMenuButton _selectedItem;
        /// <summary>
        /// The last selected item on this pie. A selected item is the currently active radio button,
        /// if a radio button is part of this pie. Null if no radio buttons are present, or a selection
        /// hasn't been made yet.
        /// </summary>
        public RadialMenuButton SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (value != null)
                {
                    SetField(ref _selectedItem, value);

                    var eventHandler = PropertyChanged;
                    eventHandler?.Invoke(this, new PropertyChangedEventArgs("SelectedItemValue"));
                }
            }
        }

        public Pie()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += (sender, args) =>
            {
                Draw();

                if (PieSlices.FirstOrDefault() != null)
                {
                    Angle = 360 - PieSlices.FirstOrDefault().Angle / 4;
                }
            };


            PieSlices.CollectionChanged += (sender, args) =>
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (PieSlice item in args.NewItems)
                        {
                            LayoutContent.Children.Add(item);
                        }
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        foreach (PieSlice item in args.OldItems)
                        {
                            LayoutContent.Children.Remove(item);
                        }
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        LayoutContent.Children.Clear();
                        break;
                }
            };
        }

        /// <summary>
        /// Draw or redraw the pie by generating PieSlices from passed RadialMenuButtons.
        /// Previously created PieSlices on this Pie will be thrown away.
        /// </summary>
        public void Draw()
        {
            PieSlices.Clear();
            var startAngle = StartAngle;

            // Draw PieSlices for each Slice Object
            foreach (var slice in Slices)
            {
                var sliceSize = 360.00 / Slices.Count;
                var pieSlice = new PieSlice
                {
                    StartAngle = startAngle,
                    Angle = sliceSize,
                    Radius = Size / 2,
                    Height = Height,
                    Width = Width,
                    OuterArcThickness =  slice.OuterArcThickness ?? SourceRadialMenu.OuterArcThickness,
                    // The defaults below use OneNote-like purple colors
                    InnerNormalColor = slice.InnerNormalColor ?? SourceRadialMenu.InnerNormalColor,
                    InnerHoverColor = slice.InnerHoverColor ?? SourceRadialMenu.InnerHoverColor,
                    InnerTappedColor = slice.InnerTappedColor ?? SourceRadialMenu.InnerTappedColor,
                    InnerReleasedColor = slice.InnerReleasedColor ?? SourceRadialMenu.InnerReleasedColor,
                    OuterNormalColor = slice.OuterNormalColor ?? SourceRadialMenu.OuterNormalColor,
                    OuterDisabledColor = slice.OuterDisabledColor ?? SourceRadialMenu.OuterDisabledColor,
                    OuterHoverColor = slice.OuterHoverColor ?? SourceRadialMenu.OuterHoverColor,
                    OuterTappedColor = slice.OuterTappedColor ?? SourceRadialMenu.OuterTappedColor,
                    // Label
                    IconSize = slice.IconSize,
                    Icon = slice.Icon,
                    IconFontFamily = slice.IconFontFamily,
                    IconForegroundBrush = slice.IconForegroundBrush,
                    IconImage = slice.IconImage,
                    IconImageSideLength = (Size / 2) * .25,
                    IsLabelHidden = slice.HideLabel,
                    Label = slice.Label,
                    LabelSize = slice.LabelSize,
                    // Original Button
                    OriginalRadialMenuButton = slice
                };

                // Allow slice to call the change request method on the radial menu
                pieSlice.ChangeMenuRequestEvent += SourceRadialMenu.ChangeMenu;
                // Allow slice to call the change selected request to clear all other radio buttons
                pieSlice.ChangeSelectedEvent += PieSlice_ChangeSelectedEvent;
                PieSlices.Add(pieSlice);
                startAngle += sliceSize;
            }

            FindSelectedPieSlice();
        }

        /// <summary>
        /// Helper method performing a manual lookup of the currently selected item (a radio button)
        /// by checking all PieSices.
        /// </summary>
        public void FindSelectedPieSlice()
        {
            foreach (var ps in PieSlices)
            {
                var ormb = ps.OriginalRadialMenuButton;
                if (ormb.Type == RadialMenuButton.ButtonType.Radio && ormb.MenuSelected)
                {
                    SelectedItem = ps.OriginalRadialMenuButton;
                }
            }
        }

        /// <summary>
        /// Helper method manually updating the visual state of all PieSlices generated from a RadialMenuButton of type "radio" or "toggle".
        /// </summary>
        public void UpdatePieSlicesVisualState()
        {
            foreach (var ps in PieSlices)
            {
                if (ps.OriginalRadialMenuButton.Type == RadialMenuButton.ButtonType.Radio) ps.UpdateSliceForRadio();
                if (ps.OriginalRadialMenuButton.Type == RadialMenuButton.ButtonType.Toggle) ps.UpdateSliceForToggle();
            }
        }

        /// <summary>
        /// Event handler for a value change on a radio button on this pie.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="slice"></param>
        private void PieSlice_ChangeSelectedEvent(object sender, PieSlice slice)
        {
            if (slice != null && slice.OriginalRadialMenuButton.Type == RadialMenuButton.ButtonType.Radio)
            {
                SelectedItem = slice.OriginalRadialMenuButton;
            }

            foreach (var ps in PieSlices.Where(ps => ps.OriginalRadialMenuButton.Type == RadialMenuButton.ButtonType.Radio 
                                                    && ps.OriginalRadialMenuButton.MenuSelected && ps.StartAngle != slice.StartAngle))
            {
                ps.OriginalRadialMenuButton.MenuSelected = false;
                ps.UpdateSliceForRadio();
            }
        }

        /// <summary>
        /// Helper method for setting fields on properties
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!value.Equals(field))
            {
                field = value;
                var eventHandler = PropertyChanged;
                eventHandler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

