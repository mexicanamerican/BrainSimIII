﻿//
// PROPRIETARY AND CONFIDENTIAL
// Brain Simulator 3 v.1.0
// © 2022 FutureAI, Inc., all rights reserved
//

using System;
using System.Collections.Generic;
//using System.Drawing;

//using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Input;


using System.Windows.Shapes;

namespace BrainSimulator.Modules
{
    public partial class ModuleVisionDlg : ModuleBaseDlg
    {
        // Constructor of the ModuleUKSStatement dialog
        public ModuleVisionDlg()
        {
            InitializeComponent();
        }

        // Draw gets called to draw the dialog when it needs refreshing
        int scale;
        public override bool Draw(bool checkDrawTimer)
        {
            if (!base.Draw(checkDrawTimer)) return false;

            ModuleVision parent = (ModuleVision)base.ParentModule;

            if (parent.bitmap == null) return false;
            try
            {
                labelProperties.Content = "Image: " + Math.Round(parent.bitmap.Width) + "x" + Math.Round(parent.bitmap.Height) +
                    "\r\nBit Depth: " + parent.bitmap.Format.BitsPerPixel +
                    "\r\nSegments: " + parent.segments.Count +
                    "\r\nCorners: " + parent.corners.Count;
            }
            catch { return false; }

            theCanvas.Children.Clear();

            scale = (int)(theCanvas.ActualHeight / parent.imageArray.GetLength(1));
            int pixelSize = scale - 2;
            if (pixelSize < 2) pixelSize = 2;

            //draw the image
            if (cbShowImage.IsChecked == true && parent.bitmap != null)
            {
                //TODO: images with bit depth < 32 display at slightly wrong scale
                Image i = new Image
                {
                    Height = parent.bitmap.Height * scale,
                    Width = parent.bitmap.Width * scale,
                    Source = (ImageSource)parent.bitmap,
                };
                Canvas.SetLeft(i, 0);
                Canvas.SetTop(i, 0);
                theCanvas.Children.Add(i);
            }

            //draw the pixels
            if (cbShowPixels.IsChecked == true && parent.imageArray != null)
            {
                for (int x = 0; x < parent.imageArray.GetLength(0); x++)
                    for (int y = 0; y < parent.imageArray.GetLength(1); y++)
                    {
                        var pixel = parent.imageArray[x, y];
                        var s = pixel.ToString();
                        if (pixel.ToString() != "#01FFFFFF")
                        { }
                        pixel.A = 255;

                        if (pixel != null)
                        {
                            //pixel.luminance /= 2;
                            SolidColorBrush b = new SolidColorBrush(pixel);
                            Rectangle e = new ()
                            {
                                Height = pixelSize,
                                Width = pixelSize,
                                Stroke = b,
                                Fill = b,
                                ToolTip = new System.Windows.Controls.ToolTip { HorizontalOffset = 50, Content = $"({(int)x},{(int)y}) {pixel}" },
                            };
                            Canvas.SetLeft(e, x * scale - pixelSize / 2);
                            Canvas.SetTop(e, y * scale - pixelSize / 2);
                            theCanvas.Children.Add(e);
                        }
                    }
            }

            //draw the boundaries
            if (cbShowBoundaries.IsChecked == true && parent.boundaryArray != null)
            {
                foreach (var pt in parent.boundaryPoints)
                //for (int x = 0; x < parent.boundaryArray.GetLength(0); x++)
                //    for (int y = 0; y < parent.boundaryArray.GetLength(1); y++)
                    {
//                        if (parent.boundaryArray[x, y] != 0)
                        {
                            Rectangle e = new ()
                            {
                                Height = pixelSize,
                                Width = pixelSize,
                                Stroke = Brushes.Black,
                                Fill = Brushes.Black,
//                                ToolTip = new System.Windows.Controls.ToolTip { HorizontalOffset = 100, Content = $"({(int)x},{(int)y})" },
                            };
                            Canvas.SetLeft(e, pt.X * scale - pixelSize / 2);
                            Canvas.SetTop(e, pt.Y * scale - pixelSize / 2);
                            theCanvas.Children.Add(e);
                        }
                    }
            }
            //if (cbShowBoundaries.IsChecked == true && parent.boundaryArray != null)
            //{
            //    for (int x = 0; x < parent.boundaryArray.GetLength(0); x++)
            //        for (int y = 0; y < parent.boundaryArray.GetLength(1); y++)
            //        {
            //            if (parent.boundaryArray[x, y] != 0)
            //            {
            //                Ellipse e = new Ellipse()
            //                {
            //                    Height = pixelSize,
            //                    Width = pixelSize,
            //                    Stroke = Brushes.Black,
            //                    Fill = Brushes.Black,
            //                    ToolTip = new System.Windows.Controls.ToolTip { HorizontalOffset = 100, Content = $"({(int)x},{(int)y})" },
            //                };
            //                Canvas.SetLeft(e, x * scale - pixelSize / 2);
            //                Canvas.SetTop(e, y * scale - pixelSize / 2);
            //                theCanvas.Children.Add(e);
            //            }
            //        }
            //}

            //draw the hough transform
            if (cbShowHough.IsChecked == true)
            {
                var acc = parent.segmentFinder.accumulator;
                int maxRho = acc.GetLength(0);
                int maxTheta = acc.GetLength(1);
                float scalex = (float)theCanvas.ActualWidth / acc.GetLength(0);
                float scaley = (float)theCanvas.ActualHeight / acc.GetLength(1);
                for (int rhoIndex = 0; rhoIndex < maxRho; rhoIndex++)
                {
                    for (int thetaIndex = 0; thetaIndex < maxTheta; thetaIndex++)
                    {
                        var votes = parent.segmentFinder.accumulator[rhoIndex, thetaIndex].Count;
                        if (votes > 19) votes = 19;
                        if (votes > 4)// && votes < 40)
                        {

//                            var maxItem = parent.segmentFinder.localMaxima.FindFirst(x => x.Item2 == rhoIndex && x.Item3 == thetaIndex);

                            HSLColor hSLColor = new HSLColor(Colors.Green);
                            float intensity = (float)votes / 20;
                            hSLColor.luminance = intensity;
                            //if (maxItem != null)
                            //{
                            //    hSLColor = new HSLColor(Colors.Red);
                            //    hSLColor.luminance = 0.5f;
                            //}
                            Brush brush1 = new SolidColorBrush(hSLColor.ToColor());
                            Rectangle e = new ()
                            {
                                Height = scaley-1,
                                Width = scalex-1,
                                Stroke = brush1,
                                Fill = brush1,
                                ToolTip = new System.Windows.Controls.ToolTip { Content = $"({(int)rhoIndex},{(int)thetaIndex})" },
                            };
                            Canvas.SetLeft(e, rhoIndex * scalex);
                            Canvas.SetTop(e, thetaIndex * scaley);
                            e.MouseEnter += E_MouseEnter;
                            theCanvas.Children.Add(e);
                        }
                    }
                }
            }
            //draw the lines
            if (cbShowLines.IsChecked == true && parent.segmentFinder.localMaxima != null & parent.segmentFinder.localMaxima.Count > 0)
            {
                for (int i = parent.segmentFinder.localMaxima.Count - 1; i >= 0; i--)
                {
                    Tuple<int, int, int, float> line = parent.segmentFinder.localMaxima[i];
                    var points = parent.segmentFinder.accumulator[line.Item2, line.Item3];
                    float maxVotes = parent.segmentFinder.localMaxima[0].Item1;
                    float votes = line.Item1;
                    float lineVotes = line.Item4;
                    int minVotes = 4;
                    if (votes < minVotes) continue;
                    float intensity = (votes - minVotes) / maxVotes;
                    intensity *= 2;
                    HSLColor hSLColor = new HSLColor(Colors.Green);
                    hSLColor.luminance = intensity;
                    Brush brush = new SolidColorBrush(hSLColor.ToColor());
                    int rhoIndex = line.Item2;
                    int rho = rhoIndex - parent.segmentFinder.maxDistance;
                    int theta = line.Item3;
                    if (theta == 0 || theta == 180) //line is vertical
                    {
                        Line l = new Line()
                        {
                            X1 = rho * scale,
                            X2 = rho * scale,
                            Y1 = 0 * scale,
                            Y2 = theCanvas.ActualHeight * scale,
                            Stroke = brush,
                            ToolTip = new System.Windows.Controls.ToolTip { HorizontalOffset = 200, Content = $"({(int)rhoIndex},{(int)theta},{(int)votes},{(int)lineVotes}) (r,t,v,vl)" },
                        };
                        theCanvas.Children.Add(l);
                    }
                    else
                    {
                        //calculate (m,b) for y=mx+b
                        double fTheta = theta * Math.PI / parent.segmentFinder.numAngles;
                        double b = rho / Math.Sin(fTheta);
                        double m = -Math.Cos(fTheta) / Math.Sin(fTheta);
                        Line l = new Line()
                        {
                            X1 = 0,
                            X2 = 1000 * scale,
                            Y1 = b * scale,
                            Y2 = (b + m * 1000) * scale,
                            Stroke = brush,
                            ToolTip = new System.Windows.Controls.ToolTip { HorizontalOffset = 200, Content = $"({(int)rhoIndex},{(int)theta},{(int)votes},{(int)lineVotes}) (r,t,v,vl)" },
                        };
                        theCanvas.Children.Add(l);
                    }
                }
            }

            //draw the segments
            if (cbShowSegments.IsChecked == true && parent.segments != null & parent.segments.Count > 0)
            {
                for (int i = 0; i < parent.segments.Count; i++)
                {
                    Segment segment = parent.segments[i];
                    Line l = new Line()
                    {
                        X1 = segment.P1.X * scale,
                        X2 = segment.P2.X * scale,
                        Y1 = segment.P1.Y * scale,
                        Y2 = segment.P2.Y * scale,
                        Stroke = Brushes.Red,
                        StrokeThickness = 4,
                        //ToolTip = new System.Windows.Controls.ToolTip { Content = $"/*{(int)segment.theColor}*/:({segment.P1.X},{segment.P1.Y}) - -({segment.P2.X},{segment.P2.Y})" },
                    };
                    theCanvas.Children.Add(l);
                }
            }

            //draw the corners
            if (cbShowCorners.IsChecked == true && parent.corners != null && parent.corners.Count > 0)
            {
                foreach (var corner in parent.corners)
                {
                    float size = 15;
                    Brush b = Brushes.White;
                    if (corner.angle == 0)
                        b = Brushes.Pink;
                    Ellipse e = new Ellipse()
                    {
                        Height = size,
                        Width = size,
                        Stroke = b,
                        Fill = b
                    };
                    Canvas.SetTop(e, corner.location.Y * scale - size / 2);
                    Canvas.SetLeft(e, corner.location.X * scale - size / 2);
                    theCanvas.Children.Add(e);
                }
            }

            return true;
        }

        Line tempLine;
        private void E_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Rectangle  e1)
            {
                if (tempLine != null)
                    theCanvas.Children.Remove(tempLine);

                var xx = e1.ToolTip.ToString();
                xx = xx[xx.IndexOf("(")..];
                xx = xx.Replace("(", "");
                xx = xx.Replace(")", "");
                string[] coords = xx.Split(",");
                int.TryParse(coords[0], out int rho);
                int.TryParse(coords[1], out int theta);
                ModuleVision parent = (ModuleVision)base.ParentModule;
                errorText.Content = $"{(int)rho},{(int)theta} : {parent.segmentFinder.accumulator[rho, theta].Count} votes";
                errorText.Foreground = new SolidColorBrush(Colors.White);
                rho = rho - parent.segmentFinder.maxDistance;

                if (theta == 0 || theta == 180)
                {
                    tempLine = new Line()
                    {
                        X1 = rho * scale,
                        X2 = rho * scale,
                        Y1 = 0 * scale,
                        Y2 = theCanvas.ActualHeight * scale,
                        Stroke = new SolidColorBrush(Colors.Blue),
                        StrokeThickness = 4,
                    };
                }
                else
                {

                    //calculate (m,b) for y=mx+b
                    double fTheta = theta * Math.PI / parent.segmentFinder.numAngles;
                    double b = rho / Math.Sin(fTheta);
                    double m = -Math.Cos(fTheta) / Math.Sin(fTheta);
                    tempLine = new Line()
                    {
                        X1 = 0,
                        X2 = 1000 * scale,
                        Y1 = b * scale,
                        Y2 = (b + m * 1000) * scale,
                        Stroke = new SolidColorBrush(Colors.Blue),
                        StrokeThickness = 4,
                    };
                }
                theCanvas.Children.Add(tempLine);
            }
        }

        string defaultDirectory = "";
        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            if (defaultDirectory == "")
            {
                defaultDirectory = System.IO.Path.GetDirectoryName(MainWindow.currentFileName);
            }
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                Filter = "Image Files| *.png;*.jpg",
                Title = "Select an image file",
                Multiselect = true,
                InitialDirectory = defaultDirectory,
            };
            // Show the Dialog.  
            // If the user clicked OK in the dialog  
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                defaultDirectory = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);
                ModuleVision parent = (ModuleVision)base.ParentModule;

                textBoxPath.Text = openFileDialog1.FileName;
                List<string> fileList;
                string curPath;
                if (openFileDialog1.FileNames.Length > 1)
                {
                    fileList = new List<string>(openFileDialog1.FileNames);
                    curPath = fileList[0];
                }
                else
                {
                    fileList = GetFileList(openFileDialog1.FileName);
                    curPath = openFileDialog1.FileName;
                }
                parent.previousFilePath = "";
                parent.currentFilePath = curPath;
                //parent.SetParameters(fileList, curPath, (bool)cbAutoCycle.IsChecked, (bool)cbNameIsDescription.IsChecked);
            }
        }

        private List<string> GetFileList(string filePath)
        {
            SearchOption subFolder = SearchOption.AllDirectories;
            //if (!(bool)cbSubFolders.IsChecked)
            //    subFolder = SearchOption.TopDirectoryOnly;
            string dir = filePath;
            FileAttributes attr = File.GetAttributes(filePath);
            if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                dir = System.IO.Path.GetDirectoryName(filePath);
            return new List<string>(Directory.EnumerateFiles(dir, "*.png", subFolder));
        }

        private void cb_Checked(object sender, RoutedEventArgs e)
        {
            Draw(false);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModuleVision parent = (ModuleVision)base.ParentModule;
            parent.previousFilePath = "";
        }

        private void ModuleBaseDlg_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw(true);
        }

        private void ModuleBaseDlg_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.R)
                Button_Click(null,null);
        } 
    }
}
