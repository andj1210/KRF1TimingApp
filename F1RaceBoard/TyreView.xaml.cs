// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Controls;
using System.Windows.Media;

namespace F1GameSessionDisplay
{
    /// <summary>
    /// Interaktionslogik für TyreView.xaml
    /// </summary>
    public partial class TyreView : UserControl
    {
        public static readonly TyreView SoftTyre;
        public static readonly TyreView MediumTyre;
        public static readonly TyreView HardTyre;
        public static readonly TyreView InterTyre;
        public static readonly TyreView WetTyre;
        public static readonly TyreView OtherTyre;

        static TyreView()
        {
            SoftTyre = new TyreView();
            SoftTyre.txt_blk_name.Text = "S";
            SoftTyre.txt_blk_name.Foreground = Brushes.Red;
            SoftTyre.ellipse_tyremark.Stroke = Brushes.Red;

            MediumTyre = new TyreView();
            MediumTyre.txt_blk_name.Text = "M";
            MediumTyre.txt_blk_name.Foreground = Brushes.Yellow;
            MediumTyre.ellipse_tyremark.Stroke = Brushes.Yellow;

            HardTyre = new TyreView();
            HardTyre.txt_blk_name.Text = "H";
            HardTyre.txt_blk_name.Foreground = Brushes.White;
            HardTyre.ellipse_tyremark.Stroke = Brushes.White;

            InterTyre = new TyreView();
            InterTyre.txt_blk_name.Text = "I";
            InterTyre.txt_blk_name.Foreground = Brushes.Green;
            InterTyre.ellipse_tyremark.Stroke = Brushes.Green;

            WetTyre = new TyreView();
            WetTyre.txt_blk_name.Text = "W";
            WetTyre.txt_blk_name.Foreground = Brushes.Blue;
            WetTyre.ellipse_tyremark.Stroke = Brushes.Blue;

            OtherTyre = new TyreView();
            OtherTyre.txt_blk_name.Text = "?"; // "classic" or unknown
            OtherTyre.txt_blk_name.Foreground = Brushes.Silver;
            OtherTyre.ellipse_tyremark.Stroke = Brushes.Silver;
        }


        public TyreView()
        {
            InitializeComponent();
        }

        // we can obviously display an object only once, so the show the same content, we need to create a copied object
        // this contructor alows to copy an existing TyreView into an new object.
        public TyreView(TyreView prototype) : this()
        {
            txt_blk_name.Text = prototype.txt_blk_name.Text;
            txt_blk_name.Foreground = prototype.txt_blk_name.Foreground;
            ellipse_tyremark.Stroke = prototype.ellipse_tyremark.Stroke;
        }
    }
}
