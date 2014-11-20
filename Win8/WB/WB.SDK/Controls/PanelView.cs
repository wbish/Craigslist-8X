using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

using WB.SDK;
using WB.SDK.Logging;

namespace WB.SDK.Controls
{
    public sealed class PanelView : Panel
    {
        public PanelView()
        {
            this._snapState.SnapOnRefresh = -1;

            this.ManipulationMode = ManipulationModes.All;
            this.ManipulationStarted += PanelView_ManipulationStarted;
            this.ManipulationCompleted += PanelView_ManipulationCompleted;
            this.ManipulationDelta += PanelView_ManipulationDelta;
            this.PointerWheelChanged += PanelView_PointerWheelChanged;
        }

        #region Control Handlers
        protected override Size MeasureOverride(Size availableSize)
        {
            Size size = new Size(Double.PositiveInfinity, availableSize.Height);

            foreach (UIElement child in Children)
            {
                child.Measure(size);
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (this.Children == null || this.Children.Count == 0)
                return finalSize;

            if (this._snapState.SnapOnRefresh != -1)
            {
                this.SnapToPanel(this._snapState.SnapOnRefresh);
                this._snapState.SnapOnRefresh = -1;
            }

            // Draw the panels
            this.ArrangePanels(finalSize);

            if (this.ItemsArranged != null)
            {
                this.ItemsArranged(this, this.Children);
            }

            //Logger.LogMessage("PanelView", "Snapped Panel: {0}. Direction: {1}", this._snapState.Panel, this._snapState.Direction);
            //Logger.LogMessage("PanelView", "Calculated Render Origin: {0}, X Displacement: {1}", this._snapState.RenderOffset, this._snapState.DisplacementX);

            return finalSize;
        }

        public void PanelView_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;
            this._snapState.Manipulated = true;
            this._snapState.InertialReleasedPanel = -1;

            if (this.ItemsMoved != null)
                this.ItemsMoved(this, PanelMove.Started);
        }

        public void PanelView_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (this._snapState.Manipulated)
            {
                e.Handled = true;

                this._snapState.DisplacementX += e.Delta.Translation.X;

                if (this._snapState.DisplacementX != 0)
                {
                    this.InvalidateArrange();
                }

                if (e.IsInertial)
                {
                    if (this._snapState.InertialReleasedPanel < 0 && Math.Abs(e.Velocities.Linear.X) >= 0.5)
                    {
                        this._snapState.IntertialDirection = e.Velocities.Linear.X > 0 ? Direction.Left : Direction.Right;
                        this._snapState.InertialReleasedPanel = this.GetPanelAtOffset(this._snapState.RenderOffset);
                    }

                    double leftMax = MaxEdgePull * -1;
                    double rightMax = this._snapState.PanelWidthSum - this.DesiredSize.Width + MaxEdgePull + (Math.Min(0, this._snapState.PanelWidthSum - this.DesiredSize.Width) * -1);

                    // If the movement is intertial and we have already reached the max edge we support, we should snap to the appropriate panel.
                    if (this._snapState.RenderOffset <= leftMax || this._snapState.RenderOffset >= rightMax)
                    {
                        e.Complete();
                    }
                }
            }
        }

        public void PanelView_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (this._snapState.Manipulated)
            {
                e.Handled = true;

                if (this._snapState.InertialReleasedPanel == this._snapState.Panel && this._snapState.InertialReleasedPanel == this.GetPanelAtOffset(this._snapState.RenderOffset))
                {
                    // We had inertia, but not enough to move to the next/previous panel
                    if (this._snapState.IntertialDirection == Direction.Left)
                    {
                        if (this._snapState.InertialReleasedPanel > 0)
                        {
                            this.SnapToPanel(this._snapState.InertialReleasedPanel - 1);
                            return;
                        }
                    }
                    else if (this._snapState.IntertialDirection == Direction.Right)
                    {
                        if (this._snapState.InertialReleasedPanel < this.Children.Count - 1)
                        {
                            this.SnapToPanel(this._snapState.InertialReleasedPanel + 1);
                            return;
                        }
                    }
                }

                this.SnapToOffset(this._snapState.RenderOffset);
            }
        }

        public void PanelView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

            if (delta != 0)
            {
                e.Handled = true;

                if (this.ItemsMoved != null)
                    this.ItemsMoved(this, PanelMove.Started);

                this._snapState.Manipulated = true;
                this._snapState.DisplacementX += delta;

                if (this._snapState.DisplacementX != 0)
                {
                    this.InvalidateArrange();
                }

                if (this._wheelTimer != null)
                    this._wheelTimer.Cancel();

                this._wheelTimer = ThreadPoolTimer.CreateTimer(
                    (source) =>
                    {
                        var x = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                            () =>
                            {
                                this.SnapToOffset(this._snapState.RenderOffset);
                            });
                    }, TimeSpan.FromMilliseconds(250));
            }
        }
        #endregion

        #region Public Methods / Events
        public event EventHandler<IEnumerable<UIElement>> ItemsArranged;
        public event EventHandler<PanelMove> ItemsMoved;

        /// <summary>
        /// Return the index of panel
        /// </summary>
        /// <param name="panel">Panel</param>
        /// <returns></returns>
        public int GetPanelIndex(UIElement panel)
        {
            for (int i = 0; i < this.Children.Count; ++i)
            {
                if (panel == this.Children[i])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Add a new panel to the PanelView control
        /// </summary>
        /// <param name="panel">Panel to be added</param>
        public async Task AddPanel(UIElement panel)
        {
            this.Children.Add(panel);

            this._snapState.SnapOnRefresh = this.Children.Count - 1;

            this.InvalidateMeasure();
            this.InvalidateArrange();

            await this.AnimateEnter(panel);
        }

        /// <summary>
        /// Remove panel
        /// </summary>
        /// <param name="panel">Panel to remove</param>
        /// <param name="recursive">Include all panels to the right</param>
        public void RemovePanel(UIElement panel, bool recursive)
        {
            this.RemovePanel(this.Children.IndexOf(panel), recursive);
        }

        /// <summary>
        /// Remove panels starting from the given index.
        /// </summary>
        /// <param name="panel">Panel index to remove</param>
        /// <param name="recursive">Include all panels to the right</param>
        public void RemovePanel(int panel, bool recursive)
        {
            if (panel >= this.Children.Count)
                return;

            int count = this.Children.Count - panel;

            for (int i = 0; i < (recursive ? count : 1); ++i)
            {
                this.Children.RemoveAt(panel);
            }            
        }

        /// <summary>
        /// Snap to panel or closest allowed while keeping request panel in view.
        /// </summary>
        /// <param name="panel">Request panel to snap to</param>
        public void SnapToPanel(UIElement panel)
        {
            for (int i = 0; i < this.Children.Count; ++i)
            {
                if (panel == this.Children[i])
                {
                    this.SnapToPanel(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Snap to panel or closest allowed while keeping request panel in view.
        /// </summary>
        /// <param name="panel">Request panel to snap to</param>
        public void SnapToPanel(int panel)
        {
            if (panel >= this.Children.Count)
                throw new IndexOutOfRangeException("Panel index.");

            if (panel + 1 == this.Children.Count)
            {
                this.SnapToOffset(int.MaxValue);
            }
            else
            {
                double widthTraversed = 0;
                for (int i = 0; i < panel; ++i)
                {
                    widthTraversed += this.Children[i].DesiredSize.Width;
                }
                this.SnapToOffset(widthTraversed);
            }
        }

        public bool PanelOutOfView(UIElement panel)
        {
            double traversedWidth = 0;

            for (int i = 0; i < this.Children.Count; ++i)
            {
                if (this.Children[i] == panel)
                {
                    if (traversedWidth + panel.DesiredSize.Width <= this._snapState.RenderOffset
                        || traversedWidth > this._snapState.RenderOffset + this.DesiredSize.Width)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                traversedWidth += this.Children[i].DesiredSize.Width;
            }

            return false;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Calculate the pixel offset from left to right we should start drawing from. The result is a 
        /// function of the snapped panel and user manipulation (displacement).
        /// </summary>
        void CalculateRenderOffset()
        {
            double widthTraversedCalc = 0;

            for (int i = 0; i < this.Children.Count; ++i)
            {
                if (this._snapState.Panel == i)
                {
                    if (this._snapState.Direction == Direction.Left)
                    {
                        this._snapState.RenderOffset = widthTraversedCalc + (this._snapState.DisplacementX * -1);
                        break;
                    }
                    else if (this._snapState.Direction == Direction.Right)
                    {
                        this._snapState.RenderOffset = widthTraversedCalc + this.Children[i].DesiredSize.Width - this.DesiredSize.Width + (this._snapState.DisplacementX * -1);
                        break;
                    }
                }
                else
                {
                    widthTraversedCalc += this.Children[i].DesiredSize.Width;
                }
            }

            // Pull to the right
            this._snapState.RenderOffset = Math.Max(this._snapState.RenderOffset, MaxEdgePull * -1);

            // Pull to the left
            this._snapState.RenderOffset = Math.Min(this._snapState.RenderOffset,
                this._snapState.PanelWidthSum - this.DesiredSize.Width + MaxEdgePull + (Math.Min(0, this._snapState.PanelWidthSum - this.DesiredSize.Width) * -1));
        }

        /// <summary>
        /// Arrange panels for drawing
        /// </summary>
        /// <param name="finalSize">Size of PanelView</param>
        void ArrangePanels(Size finalSize)
        {
            Rect finalRect = new Rect(0, 0, finalSize.Width, finalSize.Height);
            double widthTraversedExec = 0;

            if (this._snapState.Direction == Direction.Left || this._snapState.Manipulated)
            {
                // We need a render origin if we are using Left to Right drawing
                this.CalculateRenderOffset();

                for (int i = 0; i < this.Children.Count; ++i)
                {
                    var child = this.Children[i];
                    finalRect.X = widthTraversedExec - this._snapState.RenderOffset;
                    finalRect.Width = child.DesiredSize.Width;
                    child.Arrange(finalRect);
                    widthTraversedExec += child.DesiredSize.Width;
                }
            }
            else if (this._snapState.Direction == Direction.Right)
            {
                for (int i = this.Children.Count - 1; i >= 0; --i)
                {
                    var child = this.Children[i];
                    finalRect.X = this.DesiredSize.Width - widthTraversedExec - child.DesiredSize.Width;
                    finalRect.Width = child.DesiredSize.Width;
                    child.Arrange(finalRect);
                    widthTraversedExec += child.DesiredSize.Width;
                }
            }
        }

        /// <summary>
        /// Based on the pixel offset we are currently at, we calculate which panel we should snap to.
        /// We take into account user displacement, screen width and panel edges.
        /// </summary>
        /// <param name="offset">Left to right offset panels are being drawn</param>
        void CacheSnappedPanel(double offset)
        {
            this.CachePanelWidthSum();

            if (this._snapState.PanelWidthSum - offset <= this.DesiredSize.Width && this._snapState.PanelWidthSum > this.DesiredSize.Width)
            {
                this._snapState.Panel = this.Children.Count - 1;
                this._snapState.Direction = Direction.Right;
                return;
            }

            this._snapState.Direction = Direction.Left;
            double widthTraversed = 0;

            for (int i = 0; i < this.Children.Count; ++i)
            {
                UIElement child = this.Children[i];

                if (this._snapState.PanelWidthSum - widthTraversed < this.DesiredSize.Width)
                {
                    this._snapState.Panel = i;
                    return;
                }
                else if (widthTraversed + child.DesiredSize.Width >= offset)
                {
                    if (i + 1 == this.Children.Count)
                    {
                        // We are the last panel we can snap to. Pick it.
                        this._snapState.Panel = i;
                    }
                    else
                    {
                        // Here we decide if we should snap to the left most panel in view, or snap to next panel if
                        // we are very close to the edge. We need to account that if we snap right one panel it is possible 
                        // to get into a situation where there is a margin on the right side of the screen because we did 
                        // not choose to snap to the right most panel using SnapDirection.Right
                        bool snapRight = (offset > widthTraversed + (child.DesiredSize.Width - Math.Min((child.DesiredSize.Width * (EdgeSnapToNextPercent)), EdgeSnapToNextPixel)));

                        // We think we should snap right because we are near the edge. Determine if that is the right move 
                        // or if we should use SnapDirection.Right on the very last panel of the control.
                        if (snapRight && this._snapState.PanelWidthSum > this.DesiredSize.Width && this.CalculateRenderWidth(i + 1) < this.DesiredSize.Width)
                        {
                            this._snapState.Panel = this.Children.Count - 1;
                            this._snapState.Direction = Direction.Right;
                        }
                        else
                        {
                            this._snapState.Panel = i + (snapRight ? 1 : 0);
                        }
                    }
                    return;
                }
                else
                {
                    widthTraversed += child.DesiredSize.Width;
                }
            }
        }

        /// <summary>
        /// Cache the sum of the width of all child panels
        /// </summary>
        void CachePanelWidthSum()
        {
            this._snapState.PanelWidthSum = 0;

            for (int i = 0; i < this.Children.Count; ++i)
            {
                this._snapState.PanelWidthSum += this.Children[i].DesiredSize.Width;
            }
        }

        /// <summary>
        /// Animate snap to offset.
        /// </summary>
        /// <param name="offset">Offset to snap to</param>
        void SnapToOffset(double offset)
        {
            double origin = this._snapState.RenderOffset;
            this.CacheSnappedPanel(offset);

            if (this._snapState.Manipulated)
            {
                if (this.ItemsMoved != null)
                    this.ItemsMoved(this, PanelMove.Finished);
            }

            this._snapState.Manipulated = false;
            this._snapState.DisplacementX = 0;

            this.CalculateRenderOffset();
            double displacement = this._snapState.RenderOffset - origin;

            this.AnimateSnap(displacement);

            this.InvalidateArrange();
        }

        int GetPanelAtOffset(double offset)
        {
            double widthTraversed = 0;

            for (int i = 0; i < this.Children.Count; ++i)
            {
                widthTraversed += this.Children[0].DesiredSize.Width;

                if (offset < widthTraversed)
                    return i;
            }

            return 0;
        }

        void AnimateSnap(double displacement)
        {
            if (displacement == 0)
                return;

            foreach (var child in this.Children)
            {
                if (child.RenderTransform as TranslateTransform == null)
                    child.RenderTransform = new TranslateTransform();

                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation();
                doubleAnimation.From = displacement;
                doubleAnimation.To = 0.0;
                doubleAnimation.EasingFunction = new CircleEase() { EasingMode = Windows.UI.Xaml.Media.Animation.EasingMode.EaseOut };
                doubleAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(this.SnapAnimationMilliseconds));
                storyboard.Children.Add(doubleAnimation);
                Storyboard.SetTarget(doubleAnimation, child.RenderTransform);
                Storyboard.SetTargetProperty(doubleAnimation, "X");
                storyboard.Begin();
            }
        }

        async Task AnimateEnter(UIElement panel)
        {
            if (panel.RenderTransform as TranslateTransform == null)
                panel.RenderTransform = new TranslateTransform();

            Storyboard storyboard = new Storyboard();
            DoubleAnimation doubleAnimation = new DoubleAnimation();
            doubleAnimation.From = this.DesiredSize.Width;
            doubleAnimation.To = 0.0;
            doubleAnimation.EasingFunction = new CircleEase() { EasingMode = Windows.UI.Xaml.Media.Animation.EasingMode.EaseOut };
            doubleAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(this.EnterAnimationMilliseconds));
            storyboard.Children.Add(doubleAnimation);
            Storyboard.SetTarget(doubleAnimation, panel.RenderTransform);
            Storyboard.SetTargetProperty(doubleAnimation, "X");

            await storyboard.BeginAsync();
        }

        async Task AnimateRemove(UIElement panel)
        {
            if (panel.RenderTransform as ScaleTransform == null)
                panel.RenderTransform = new ScaleTransform();

            Storyboard storyboard = new Storyboard();
            DoubleAnimation doubleAnimation = new DoubleAnimation();
            doubleAnimation.From = 1;
            doubleAnimation.To = 0.0;
            doubleAnimation.EasingFunction = new CircleEase() { EasingMode = Windows.UI.Xaml.Media.Animation.EasingMode.EaseOut };
            doubleAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(this.EnterAnimationMilliseconds));
            storyboard.Children.Add(doubleAnimation);
            Storyboard.SetTarget(doubleAnimation, panel.RenderTransform);
            Storyboard.SetTargetProperty(doubleAnimation, "ScaleX");

            await storyboard.BeginAsync();
        }

        double CalculateRenderWidth(int panel)
        {
            double width = 0;
            for (int i = panel; i < this.Children.Count; ++i)
            {
                width += this.Children[i].DesiredSize.Width;
            }
            return width;
        }
        #endregion

        #region Properties
        public double EdgeSnapToNextPixel
        {
            get { return (double)GetValue(EdgeSnapToNextPixelProperty); }
            set { SetValue(EdgeSnapToNextPixelProperty, value); }
        }
        public static readonly DependencyProperty EdgeSnapToNextPixelProperty =
            DependencyProperty.Register("EdgeSnapToNextPixel", typeof(double), typeof(PanelView), new PropertyMetadata(100.0));

        public double EdgeSnapToNextPercent
        {
            get { return (double)GetValue(EdgeSnapToNextPercentProperty); }
            set { SetValue(EdgeSnapToNextPercentProperty, value); }
        }
        public static readonly DependencyProperty EdgeSnapToNextPercentProperty =
            DependencyProperty.Register("EdgeSnapToNextPercent", typeof(double), typeof(PanelView), new PropertyMetadata(0.33));

        public int SnapAnimationMilliseconds
        {
            get { return (int)GetValue(SnapAnimationMillisecondsProperty); }
            set { SetValue(SnapAnimationMillisecondsProperty, value); }
        }
        public static readonly DependencyProperty SnapAnimationMillisecondsProperty =
            DependencyProperty.Register("SnapAnimationMilliseconds", typeof(int), typeof(PanelView), new PropertyMetadata(300));

        public int EnterAnimationMilliseconds
        {
            get { return (int)GetValue(EnterAnimationMillisecondsProperty); }
            set { SetValue(EnterAnimationMillisecondsProperty, value); }
        }
        public static readonly DependencyProperty EnterAnimationMillisecondsProperty =
            DependencyProperty.Register("EnterAnimationMilliseconds", typeof(int), typeof(PanelView), new PropertyMetadata(300));
        
        public double MaxEdgePull
        {
            get { return (double)GetValue(MaxEdgePullProperty); }
            set { SetValue(MaxEdgePullProperty, value); }
        }
        public static readonly DependencyProperty MaxEdgePullProperty =
            DependencyProperty.Register("MaxEdgePull", typeof(double), typeof(PanelView), new PropertyMetadata(100.0));
        #endregion

        private PanelSnapState _snapState;
        private ThreadPoolTimer _wheelTimer;
    }

    public enum PanelMove
    {
        Started,
        Finished,
    }

    struct PanelSnapState
    {
        internal int Panel;
        internal Direction Direction;

        internal bool Manipulated;
        internal double DisplacementX;

        internal int InertialReleasedPanel;
        internal Direction IntertialDirection;

        internal double PanelWidthSum;
        internal double RenderOffset;

        internal int SnapOnRefresh;
    }

    enum Direction
    {
        Left,
        Right,
    }
}