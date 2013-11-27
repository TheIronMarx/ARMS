using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Shapes;

namespace KinectExperiments
{
    // A class that holds the necessary components to display and handle a rectangle in ARMS
    class Box
    {
        private Rectangle rectangle;
        private double left, top;
        private System.Windows.Media.SolidColorBrush color;
        public bool isMoving, isStretching, isScaling = false;

        public Rectangle BoxRect
        {
            get { return rectangle; }
            set { rectangle = value; }
        }
        public double Left
        {
            get { return left; }
            set { left = value; }
        }
        public double Top
        {
            get { return top; }
            set { top = value; }
        }
        public double AspectRatio
        {
            get { return BoxRect.Width / BoxRect.Height; }
        }
        public System.Windows.Media.SolidColorBrush Color
        {
            get { return color; }
            set
            {
                rectangle.Fill = value;
                color = value;
            }
        }

        public Box()
        {
            rectangle = new Rectangle();
            rectangle.Stroke = System.Windows.Media.Brushes.FloralWhite;
            rectangle.Fill = System.Windows.Media.Brushes.DodgerBlue;
            rectangle.Opacity = 0.8;
            rectangle.Width = 75f;
            rectangle.Height = 75f;
            rectangle.MinWidth = 20f;
            rectangle.MinHeight = 20f;
            rectangle.StrokeThickness = 3;

            color = System.Windows.Media.Brushes.DodgerBlue;
        }

        // A method that returns a copy of this Box
        public Box Clone()
        {
            Box clone = new Box();
            clone.BoxRect = new Rectangle();
            clone.BoxRect.Stroke = System.Windows.Media.Brushes.FloralWhite;
            clone.BoxRect.Fill = color;
            clone.BoxRect.Opacity = 0.8;
            clone.BoxRect.Width = this.BoxRect.Width;
            clone.BoxRect.Height = this.BoxRect.Height;
            clone.BoxRect.MinWidth = 20f;
            clone.BoxRect.MinHeight = 20f;
            clone.BoxRect.StrokeThickness = 3;
            clone.color = this.color;

            clone.Left = this.Left;
            clone.Top = this.Top;
            //clone.isMoving = false;
            //clone.isStretching = false;

            return clone;
        }
    }
}
