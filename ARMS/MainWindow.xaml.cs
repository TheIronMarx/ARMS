using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using Microsoft.Research.Kinect.Audio;
using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;

// Authors:  Scott Rotvold, Sam Stutsman, and Travis Wiertzema
namespace KinectExperiments
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Runtime nui; // The Kinect natural user interface
        int trackedSkeletonsCount; // The number of skeletons that are recognized
        SkeletonData[] skeletons;

        private int canvasWidth = 617;
        private int canvasHeight = 463;
        private float prevLeftX, prevLeftY, prevRightX, prevRightY;
            // Variables for the purpose of finding distance moved between skeleton frames

        Box box1, prevBox1; // box1 = the box on screen; prevBox1 = a box saved for undoing
        Ellipse rightCircle, leftCircle; // The cursor displayed on the hands

        private KinectAudioSource audioSource;
        private const string RecognizerId = "SR_MS_en-US_Kinect_10.0";
        private SpeechRecognitionEngine sre;
        private Thread audioThread;

        private String phase = "neutral"; // Variable to tell which phase ARMS is currently in

        private int padding = 15; // Leeway for hand tracking

        public MainWindow()
        {
            box1 = new Box();
            box1.Left = canvasWidth / 2 - box1.BoxRect.Width / 2;
            box1.Top = canvasHeight / 2 - box1.BoxRect.Height / 2;

            prevBox1 = new Box();
            prevBox1.Left = canvasWidth / 2 - prevBox1.BoxRect.Width / 2;
            prevBox1.Top = canvasHeight / 2 - prevBox1.BoxRect.Height / 2;

            rightCircle = new Ellipse();
            rightCircle.Stroke = System.Windows.Media.Brushes.MediumBlue;
            rightCircle.Fill = System.Windows.Media.Brushes.Black;
            rightCircle.Opacity = 0.8;
            rightCircle.StrokeThickness = 5;
            rightCircle.Width = 20f;
            rightCircle.Height = 20f;

            leftCircle = new Ellipse();
            leftCircle.Stroke = System.Windows.Media.Brushes.Maroon;
            leftCircle.Fill = System.Windows.Media.Brushes.Black;
            leftCircle.Opacity = 0.8;
            leftCircle.StrokeThickness = 5;
            leftCircle.Width = 20f;
            leftCircle.Height = 20f;

            InitializeComponent();
            setupKinect();
        }

        private void setupKinect()
        {
            if (Runtime.Kinects.Count == 0)
            {
                this.Title = "No Kinect detected";
            }
            else
            {
                nui = Runtime.Kinects[0];

                nui.Initialize(RuntimeOptions.UseColor | RuntimeOptions.UseSkeletalTracking);

                // Callbacks for when frames are ready
                nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);
                nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);

                nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);

                nui.SkeletonEngine.TransformSmooth = true;
                
                // Remainder of the method is for initializing the audio recognition
                RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers().Where(r => r.Id == RecognizerId).FirstOrDefault();
                sre = new SpeechRecognitionEngine(ri.Id);

                // The words to recognize
                var choices = new Choices();
                choices.Add("stretch");
                choices.Add("transport");
                choices.Add("rotate");
                choices.Add("scale");
                choices.Add("transform");
                choices.Add("color");
                choices.Add("white");
                choices.Add("black");
                choices.Add("red");
                choices.Add("green");
                choices.Add("blue");
                choices.Add("stop");
                choices.Add("reset");
                choices.Add("undo");
                choices.Add("terminate");

                var gb = new GrammarBuilder();
                // Specify the culture to match the in case we are running in a different culture.
                gb.Culture = ri.Culture;
                gb.Append(choices);

                // Create the actual Grammar instance, and then load it into the speech recognizer.
                var g = new Grammar(gb);
                sre.LoadGrammar(g);

                // Callbacks for when speech is processed
                sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(sre_SpeechRecognized);
                sre.SpeechHypothesized += new EventHandler<SpeechHypothesizedEventArgs>(sre_SpeechHypothesized);
                sre.SpeechRecognitionRejected += new EventHandler<SpeechRecognitionRejectedEventArgs>(sre_SpeechRecognitionRejected);

                labelSpeech.Content = "Audio thread starting...";
                audioThread = new Thread(startAudioListening);

                audioThread.Start();
            }
        }

        private void startAudioListening()
        {
            audioSource = new KinectAudioSource();
            audioSource.FeatureMode = true;
            audioSource.AutomaticGainControl = true;
            audioSource.SystemMode = SystemMode.OptibeamArrayOnly;

            Stream aStream = audioSource.Start();
            sre.SetInputToAudioStream(aStream,
                                        new SpeechAudioFormatInfo(
                                            EncodingFormat.Pcm, 16000, 16, 1,
                                            32000, 2, null));

            sre.RecognizeAsync(RecognizeMode.Multiple);
        }

        void sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            double confidence = 0.85;

            if (e.Result.Text.ToLower() == "transport" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"transport\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    transportPhase();
                }
            }
            else if (e.Result.Text.ToLower() == "stretch" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"stretch\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    stretchPhase();
                }
            }
            else if (e.Result.Text.ToLower() == "scale" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"scale\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    scalePhase();
                }
            }
            else if (e.Result.Text.ToLower() == "color" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"color\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    colorPhase();
                }
            }
            else if (phase == "color" && e.Result.Text.ToLower() == "white" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"white\" with " + (100 * e.Result.Confidence) + "% confidence";
                box1.Color = System.Windows.Media.Brushes.White;
            }
            else if (phase == "color" && e.Result.Text.ToLower() == "black" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"black\" with " + (100 * e.Result.Confidence) + "% confidence";
                box1.Color = System.Windows.Media.Brushes.Black;
            }
            else if (phase == "color" && e.Result.Text.ToLower() == "red" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"red\" with " + (100 * e.Result.Confidence) + "% confidence";
                box1.Color = System.Windows.Media.Brushes.Red;
            }
            else if (phase == "color" && e.Result.Text.ToLower() == "green" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"green\" with " + (100 * e.Result.Confidence) + "% confidence";
                box1.Color = System.Windows.Media.Brushes.Green;
            }
            else if (phase == "color" && e.Result.Text.ToLower() == "blue" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"blue\" with " + (100 * e.Result.Confidence) + "% confidence";
                box1.Color = System.Windows.Media.Brushes.DodgerBlue;
            }
            else if (e.Result.Text.ToLower() == "stop" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"stop\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase != "netrual")
                {
                    phase = "neutral";
                    leftCircle.Fill = System.Windows.Media.Brushes.Black;
                    rightCircle.Fill = System.Windows.Media.Brushes.Black;

                    box1.isMoving = box1.isStretching = box1.isScaling = false;

                    labelTransport.Opacity = 1;
                    labelStretch.Opacity = 1;
                    labelColor.Opacity = 1;
                    labelScale.Opacity = 1;
                    labelColorWhite.Opacity = 0;
                    labelColorBlack.Opacity = 0;
                    labelColorRed.Opacity = 0;
                    labelColorGreen.Opacity = 0;
                    labelColorBlue.Opacity = 0;

                    labelReset.Opacity = 1;
                    labelUndo.Opacity = 1;
                    labelTerminate.Opacity = 1;
                }
            }
            else if (e.Result.Text.ToLower() == "undo" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"undo\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    box1 = prevBox1.Clone();
                }
            }
            else if (e.Result.Text.ToLower() == "reset" && e.Result.Confidence >= confidence)
            {
                labelSpeech.Content = "Recognized \"reset\" with " + (100 * e.Result.Confidence) + "% confidence";
                if (phase == "neutral")
                {
                    box1 = new Box();
                    box1.Left = canvasWidth / 2 - box1.BoxRect.Width / 2;
                    box1.Top = canvasHeight / 2 - box1.BoxRect.Height / 2;
                }
            }
            else if (e.Result.Text.ToLower() == "terminate" && e.Result.Confidence >= confidence + 0.05)
            {
                if (phase == "neutral")
                {
                    this.Close();
                }
            }
        }

        // Methods to set up for the phases
        private void transportPhase()
        {
            phase = "transport";
            leftCircle.Fill = System.Windows.Media.Brushes.White;
            rightCircle.Fill = System.Windows.Media.Brushes.White;

            prevBox1 = box1.Clone();

            labelTransport.Opacity = 1;
            labelStretch.Opacity = 0.5;
            labelColor.Opacity = 0.5;
            labelScale.Opacity = 0.5;

            labelReset.Opacity = 0.5;
            labelUndo.Opacity = 0.5;
            labelTerminate.Opacity = 0.5;
        }

        private void stretchPhase()
        {
            phase = "stretch";
            leftCircle.Fill = System.Windows.Media.Brushes.White;
            rightCircle.Fill = System.Windows.Media.Brushes.White;

            prevBox1 = box1.Clone();

            labelTransport.Opacity = 0.5;
            labelStretch.Opacity = 1;
            labelColor.Opacity = 0.5;
            labelScale.Opacity = 0.5;

            labelReset.Opacity = 0.5;
            labelUndo.Opacity = 0.5;
            labelTerminate.Opacity = 0.5;
        }

        private void scalePhase()
        {
            phase = "scale";
            leftCircle.Fill = System.Windows.Media.Brushes.White;
            rightCircle.Fill = System.Windows.Media.Brushes.White;

            prevBox1 = box1.Clone();

            labelTransport.Opacity = 0.5;
            labelStretch.Opacity = 0.5;
            labelColor.Opacity = 0.5;
            labelScale.Opacity = 1;

            labelReset.Opacity = 0.5;
            labelUndo.Opacity = 0.5;
            labelTerminate.Opacity = 0.5;
        }

        private void colorPhase()
        {
            phase = "color";
            leftCircle.Fill = System.Windows.Media.Brushes.White;
            rightCircle.Fill = System.Windows.Media.Brushes.White;

            prevBox1 = box1.Clone();

            labelTransport.Opacity = 0.5;
            labelStretch.Opacity = 0.5;
            labelScale.Opacity = 0.5;
            labelColor.Opacity = 1;
            labelColorWhite.Opacity = 1;
            labelColorBlack.Opacity = 1;
            labelColorRed.Opacity = 1;
            labelColorGreen.Opacity = 1;
            labelColorBlue.Opacity = 1;

            labelReset.Opacity = 0.5;
            labelUndo.Opacity = 0.5;
            labelTerminate.Opacity = 0.5;
        }

        void sre_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            labelSpeech.Content = "Speech hypothesized";
        }

        void sre_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            labelSpeech.Content = "Speech rejected";
        }

        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            imgKinectCanvas.Source = e.ImageFrame.ToBitmapSource();
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var trackedSkeletons = from s in e.SkeletonFrame.Skeletons
                                   where s.TrackingState == SkeletonTrackingState.Tracked
                                   select s;
            trackedSkeletonsCount = trackedSkeletons.Count();

            skeletons = new SkeletonData[trackedSkeletonsCount];
            for (int i = 0; i < trackedSkeletonsCount; i++)
            {
                skeletons[i] = trackedSkeletons.ElementAt(i);
            }

            // If a skeleton was recognized, get the first one to work with
            if (skeletons.Length > 0 && skeletons[0] != null)
            {
                // Grab the left hand coordinates and scale them to the canvas
                var leftHand = skeletons[0].Joints[JointID.HandLeft];
                var scaledLeftHand = leftHand.ScaleTo((int)canvasKinect.ActualWidth, (int)canvasKinect.ActualHeight, 1f, 1f);
                float leftX = scaledLeftHand.Position.X;
                float leftY = scaledLeftHand.Position.Y;
                float leftZ = scaledLeftHand.Position.Z;

                // Grab the right hand coordinates and scale them to the canvas
                var rightHand = skeletons[0].Joints[JointID.HandRight];
                var scaledRightHand = rightHand.ScaleTo((int) canvasKinect.ActualWidth, (int) canvasKinect.ActualHeight, 1f, 1f);
                float rightX = scaledRightHand.Position.X;
                float rightY = scaledRightHand.Position.Y;
                float rightZ = scaledRightHand.Position.Z;

                draw(leftX, leftY, rightX, rightY);

                // Stretch phase logic
                if (phase == "stretch" && !box1.isStretching)
                {
                    bool isLeftOnLeft = isOnLeftSide(leftX, leftY);
                    bool isLeftOnTop = isOnTopSide(leftX, leftY);
                    bool isLeftOnBottom = isOnBottomSide(leftX, leftY);
                    bool isRightOnRight = isOnRightSide(rightX, rightY);
                    bool isRightOnTop = isOnTopSide(rightX, rightY);
                    bool isRightOnBottom = isOnBottomSide(rightX, rightY);

                    if ((isLeftOnLeft && isRightOnRight)
                        || (isLeftOnTop && isRightOnBottom)
                        || (isLeftOnBottom && isRightOnTop))
                    {
                        box1.isStretching = true;
                    }
                }
                else if (phase == "stretch" && box1.isStretching)
                {
                    // If the left hand was on the left side and the right hand was on the right side
                    if (isOnLeftSide(leftX, leftY) && isOnRightSide(rightX, rightY))
                    {
                        box1.Left = leftX;
                        box1.BoxRect.Width += prevLeftX - leftX;
                        box1.BoxRect.Width += rightX - prevRightX;
                    }
                    // If the left hand was on the bottom and the right hand was on the right top
                    else if (isOnBottomSide(leftX, leftY) && isOnTopSide(rightX, rightY))
                    {
                        box1.Top = rightY;
                        box1.BoxRect.Height += prevRightY - rightY;
                        box1.BoxRect.Height += leftY - prevLeftY;
                    }
                    // If the left hand was on the top and the right hand was on the bottom
                    else if (isOnTopSide(leftX, leftY) && isOnBottomSide(rightX, rightY))
                    {
                        box1.Top = leftY;
                        box1.BoxRect.Height += prevLeftY - leftY;
                        box1.BoxRect.Height += rightY - prevRightY;
                    }
                }

                // Transport phase logic
                if (phase == "transport" && !box1.isMoving)
                {
                    // If the left hand was on the left side and the right hand was on the right side
                    bool leftValid = isOnLeftSide(leftX, leftY);
                    bool rightValid = isOnRightSide(rightX, rightY);

                    if (leftValid && rightValid)
                    {
                        box1.isMoving = true;
                    }
                }
                else if (phase == "transport" && box1.isMoving)
                {
                    double centerX = (rightX + leftX) / 2;
                    double centerY = (rightY + leftY) / 2;

                    box1.Left = centerX - box1.BoxRect.Width / 2;
                    box1.Top = centerY - box1.BoxRect.Height / 2;
                }

                // Scale phase logic
                if (phase == "scale" && !box1.isScaling)
                {
                    bool isLeftOnLeft = isOnLeftSide(leftX, leftY);
                    bool isLeftOnTop = isOnTopSide(leftX, leftY);
                    bool isLeftOnBottom = isOnBottomSide(leftX, leftY);
                    bool isRightOnRight = isOnRightSide(rightX, rightY);
                    bool isRightOnTop = isOnTopSide(rightX, rightY);
                    bool isRightOnBottom = isOnBottomSide(rightX, rightY);

                    // If the left hand was on the left side and the right hand was on the right side
                    // or if the left hand was on the bottom and the right hand was on the right top
                    // or if the left hand was on the top and the right hand was on the bottom
                    if ((isLeftOnLeft && isRightOnRight)
                        || (isLeftOnTop && isRightOnBottom)
                        || (isLeftOnBottom && isRightOnTop))
                    {
                        box1.isScaling = true;
                    }
                }
                else if (phase == "scale" && box1.isScaling)
                {
                    // If the left hand was on the left side and the right hand was on the right side
                    if (isOnLeftSide(leftX, leftY) && isOnRightSide(rightX, rightY))
                    {
                        double newWidth = box1.BoxRect.Width;
                        double newHeight = box1.BoxRect.Height;

                        box1.Left = leftX;
                        newWidth += prevLeftX - leftX;
                        newWidth += rightX - prevRightX;
                        box1.Top -= (prevLeftX - leftX) / box1.AspectRatio;
                        box1.Top -= (rightX - prevRightX) / box1.AspectRatio;
                        newHeight += (prevLeftX - leftX) / box1.AspectRatio;
                        newHeight += (rightX - prevRightX) / box1.AspectRatio;

                        box1.BoxRect.Width = newWidth;
                        box1.BoxRect.Height = newHeight;
                    }
                    // If the left hand was on the bottom and the right hand was on the right top
                    else if (isOnBottomSide(leftX, leftY) && isOnTopSide(rightX, rightY))
                    {
                        double newWidth = box1.BoxRect.Width;
                        double newHeight = box1.BoxRect.Height;

                        box1.Top = rightY;
                        newHeight += prevRightY - rightY;
                        newHeight += leftY - prevLeftY;
                        box1.Left -= (leftY - prevLeftY) * box1.AspectRatio;
                        box1.Left -= (prevRightY - rightY) * box1.AspectRatio;
                        newWidth += (leftY - prevLeftY) * box1.AspectRatio;
                        newWidth += (prevRightY - rightY) * box1.AspectRatio;

                        box1.BoxRect.Width = newWidth;
                        box1.BoxRect.Height = newHeight;
                    }
                    // If the left hand was on the top and the right hand was on the bottom
                    else if (isOnTopSide(leftX, leftY) && isOnBottomSide(rightX, rightY))
                    {
                        double newWidth = box1.BoxRect.Width;
                        double newHeight = box1.BoxRect.Height;

                        box1.Top = leftY;
                        newHeight += prevLeftY - leftY;
                        newHeight += rightY - prevRightY;
                        box1.Left -= (prevLeftY - leftY) * box1.AspectRatio;
                        box1.Left -= (rightY - prevRightY) * box1.AspectRatio;
                        newWidth += (rightY - prevRightY) * box1.AspectRatio;
                        newWidth += (prevLeftY - leftY) * box1.AspectRatio;

                        box1.BoxRect.Width = newWidth;
                        box1.BoxRect.Height = newHeight;
                    }
                }

                prevLeftX = leftX;
                prevLeftY = leftY;
                prevRightX = rightX;
                prevRightY = rightY;
            }
        }

        // Check to see if the coordinates passed are within the left bounds
        bool isOnLeftSide(float handX, float handY)
        {
            // If hand is within left side bounds
            if (handX > Canvas.GetLeft(canvasKinect.Children[1]) - padding
                && handX < Canvas.GetLeft(canvasKinect.Children[1]) + padding
                && handY > Canvas.GetTop(canvasKinect.Children[1]) - padding
                && handY < (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height) + padding)
            {
                Line leftHighlight = new Line();
                leftHighlight.X1 = Canvas.GetLeft(canvasKinect.Children[1]);
                leftHighlight.Y1 = Canvas.GetTop(canvasKinect.Children[1]);
                leftHighlight.X2 = Canvas.GetLeft(canvasKinect.Children[1]);
                leftHighlight.Y2 = (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height);
                leftHighlight.Stroke = System.Windows.Media.Brushes.Yellow;
                leftHighlight.StrokeThickness = 3;
                canvasKinect.Children.Add(leftHighlight);

                return true;
            }
            else
            {
                return false;
            }
        }

        // Check to see if the coordinates passed are within the right bounds
        bool isOnRightSide(float handX, float handY)
        {
            // If hand is within right side bounds
            if (handX > (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width) - padding
                && handX < (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width) + padding
                && handY > Canvas.GetTop(canvasKinect.Children[1]) - padding
                && handY < (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height) + padding)
            {
                Line rightHighlight = new Line();
                rightHighlight.X1 = (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width);
                rightHighlight.Y1 = Canvas.GetTop(canvasKinect.Children[1]);
                rightHighlight.X2 = (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width);
                rightHighlight.Y2 = (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height);
                rightHighlight.Stroke = System.Windows.Media.Brushes.Yellow;
                rightHighlight.StrokeThickness = 3;
                canvasKinect.Children.Add(rightHighlight);

                return true;
            }
            else
            {
                return false;
            }
        }

        // Check to see if the coordinates passed are within the top bounds
        bool isOnTopSide(float handX, float handY)
        {
            // If hand is within top side bounds
            if (handX > (Canvas.GetLeft(canvasKinect.Children[1]) - box1.BoxRect.Width) - padding
                && handX < (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width) + padding
                && handY > Canvas.GetTop(canvasKinect.Children[1]) - padding
                && handY < Canvas.GetTop(canvasKinect.Children[1]) + padding)
            {
                Line topHighlight = new Line();
                topHighlight.X1 = Canvas.GetLeft(canvasKinect.Children[1]);
                topHighlight.Y1 = Canvas.GetTop(canvasKinect.Children[1]);
                topHighlight.X2 = (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width);
                topHighlight.Y2 = Canvas.GetTop(canvasKinect.Children[1]);
                topHighlight.Stroke = System.Windows.Media.Brushes.Yellow;
                topHighlight.StrokeThickness = 3;
                canvasKinect.Children.Add(topHighlight);

                return true;
            }
            else
            {
                return false;
            }
        }

        // Check to see if the coordinates passed are within the bottom bounds
        bool isOnBottomSide(float handX, float handY)
        {
            // If hand is within bottom side bounds
            if (handX > Canvas.GetLeft(canvasKinect.Children[1]) - padding
                && handX < (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width) + padding
                && handY > (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height) - padding
                && handY < (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height) + padding)
            {
                Line bottomHighlight = new Line();
                bottomHighlight.X1 = Canvas.GetLeft(canvasKinect.Children[1]);
                bottomHighlight.Y1 = (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height);
                bottomHighlight.X2 = (Canvas.GetLeft(canvasKinect.Children[1]) + box1.BoxRect.Width);
                bottomHighlight.Y2 = (Canvas.GetTop(canvasKinect.Children[1]) + box1.BoxRect.Height);
                bottomHighlight.Stroke = System.Windows.Media.Brushes.Yellow;
                bottomHighlight.StrokeThickness = 3;
                canvasKinect.Children.Add(bottomHighlight);

                return true;
            }
            else
            {
                return false;
            }
        }

        // Draw the box and the cursors
        void draw(float leftX, float leftY, float rightX, float rightY)
        {
            // Remove previous graphics, if they exist
            while (canvasKinect.Children.Count > 1)
            {
                canvasKinect.Children.RemoveAt(1);
            }

            // Add a Rectangle Element
            Canvas.SetLeft(box1.BoxRect, box1.Left);
            Canvas.SetTop(box1.BoxRect, box1.Top);
            canvasKinect.Children.Add(box1.BoxRect);

            // Add an Ellipse as a cursor for the right hand
            Canvas.SetTop(rightCircle, rightY - rightCircle.Height / 2);
            Canvas.SetLeft(rightCircle, rightX - rightCircle.Width / 2);
            canvasKinect.Children.Add(rightCircle);

            // Add an Ellipse as a cursor for the left hand
            Canvas.SetTop(leftCircle, leftY - leftCircle.Height / 2);
            Canvas.SetLeft(leftCircle, leftX - leftCircle.Width / 2);
            canvasKinect.Children.Add(leftCircle);
        }

        // Moves the camera up
        private void buttonAngleUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nui.NuiCamera.ElevationAngle += 5;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (ArgumentOutOfRangeException outOfRangeException)
            {
                //Elevation angle must be between Elevation Minimum/Maximum"
                MessageBox.Show(outOfRangeException.Message);
            }
        }

        // Moves the camera down
        private void buttonAngleDown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                nui.NuiCamera.ElevationAngle -= 5;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (ArgumentOutOfRangeException outOfRangeException)
            {
                //Elevation angle must be between Elevation Minimum/Maximum"
                MessageBox.Show(outOfRangeException.Message);
            }
        }
    }
}
