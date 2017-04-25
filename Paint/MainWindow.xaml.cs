﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Paint.Serializer;
using Paint.Serializer.Implementations;
using Paint.ShapeList.Implementations;
using Shape;
using Shape.Heirs;
using Shape.Interfaces;
using MyShape = Shape.Shape;

namespace Paint
{
    public partial class MainWindow : Window
    {
        public static ShapeParams Param;

        private ShapeDownloader shapeDownloader;
        private Painter shapeList;
        private Painter painter;
        private ISerializer<Painter> serializer;
        private OpenFileDialog openFileDialog;
        private SaveFileDialog saveFileDialog;
        private Point lastPoint;

        private bool isCreatedShape;
        private bool isFillPointer = true;
        private bool isPaintNow;
        private bool isWidthShape;
        private int lastShapeIndex;
        private int pointsCount;

        private MyShape editedShape;

        public MainWindow()
        {
            InitializeComponent();
            ConfigurationSettings.Download(this);
            InitializeParams();
            shapeDownloader.Download();
        }

        private void InitializeParams()
        {
            shapeDownloader = new ShapeDownloader(ComboBoxShape);
            ComboBoxItemDefault.IsSelected = true;
            painter = new Painter(CanvasPaint);
            shapeList = painter;
            serializer = new JsonSerializer<Painter>(shapeDownloader);
            openFileDialog = new OpenFileDialog();
            saveFileDialog = new SaveFileDialog();
            Param = new ShapeParams
            {
                Fill = TextBlockFill.Foreground,
                Stroke = TextBlockContour.Foreground,
                StrokeThickness = ((Line) ((ComboBoxItem) ComboBoxStrokeThickness.SelectedItem).Content).StrokeThickness
            };
        }

        private void ShowError(Exception exc)
        {
            MessageBox.Show(exc.Message, "ERROR");
        }

        private void ClickPointerTextBlock(object sender, RoutedEventArgs e)
        {
            var content = ((TextBlock)sender).Name;
            if (isFillPointer && content != "TextBlockFill" || !isFillPointer && content == "TextBlockFill")
            {
                isFillPointer = content == "TextBlockFill";
                ChangeActivOfPointers();
            }
        }

        private void ChangeActivOfPointers()
        {
            var fontWeight = TextBlockContour.FontWeight;
            TextBlockContour.FontWeight = TextBlockFill.FontWeight;
            TextBlockFill.FontWeight = fontWeight;
        }

        private void ClickColorButton(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var column = button.GetValue(Grid.ColumnProperty);
            if (isFillPointer)
            {
                TextBlockFill.SetValue(Grid.ColumnProperty, column);
                TextBlockFill.Foreground = Param.Fill = button.Background;
            }
            else
            {
                TextBlockContour.SetValue(Grid.ColumnProperty, column);
                TextBlockContour.Foreground = Param.Stroke = button.Background;
            }
        }

        private void ChangeShape(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var index = comboBox.SelectedIndex;
            if (shapeDownloader.Shapes[index] is WidthShape)
            {
                ChangeStackPanelVisibility(StackPanelPosition, StackPanelPoints);
                CleanWidthTextBoxes();
            }
            else
            {
                ChangeStackPanelVisibility(StackPanelPoints, StackPanelPosition);
                CleanPointsTextBoxes();
            }
            isPaintNow = false;
            isWidthShape = StackPanelPoints.Visibility == Visibility.Hidden;
        }

        private void ChangeStackPanelVisibility(StackPanel visible, StackPanel hidden)
        {
            visible.Visibility = Visibility.Visible;
            hidden.Visibility = Visibility.Hidden;
        }

        private void CleanWidthTextBoxes()
        {
            TextBoxX.Text = TextBoxY.Text = TextBoxWidth.Text = TextBoxHeight.Text = TextBoxAngle.Text = "";
        }

        private void CleanPointsTextBoxes()
        {
            StackPanelX.Children.Clear();
            StackPanelY.Children.Clear();
            StackPanelX.Children.Add(CreatePointTextBox());
            StackPanelY.Children.Add(CreatePointTextBox());
        }

        private void ChangeWidthTextBoxes(object sender, TextChangedEventArgs e)
        {
            var maxHeight = CanvasPaint.ActualHeight;
            var maxWidth = CanvasPaint.ActualWidth;

            ButtonAddShape.IsEnabled = CheckIsStrPositiveNumber(TextBoxAngle.Text) && CheckIsStrPositiveNumber(TextBoxHeight.Text)
            && CheckIsStrPositiveNumber(TextBoxWidth.Text) && CheckIsStrPositiveNumber(TextBoxX.Text) && CheckIsStrPositiveNumber(TextBoxY.Text)
            && Convert.ToDouble(TextBoxX.Text) < maxWidth && Convert.ToDouble(TextBoxY.Text) < maxHeight && shapeDownloader.Factories.Count > 0;

            ShowTempShape();
        }

        private void ChangePointsTextBoxes(object sender, TextChangedEventArgs e)
        {
            if (shapeDownloader.Factories.Count <= 0)
                return;
            var textBoxesX = StackPanelX.Children.OfType<TextBox>().ToList();
            var textBoxesY = StackPanelY.Children.OfType<TextBox>().ToList();
            bool isCorrect = true;
            int last = textBoxesX.Count - 1;

            for (int i = 0; i < last && isCorrect; i++)
                isCorrect = CheckIsStrPositiveNumber(textBoxesX[i].Text) && CheckIsStrPositiveNumber(textBoxesY[i].Text);

            if (isCorrect && CheckIsStrPositiveNumber(textBoxesX[last].Text) && CheckIsStrPositiveNumber(textBoxesY[last].Text))
            {
                if (last > 0)
                    ButtonAddShape.IsEnabled = true;
                StackPanelX.Children.Add(CreatePointTextBox());
                StackPanelY.Children.Add(CreatePointTextBox());
                ScrollViewerPoints.ScrollToEnd();
            }
            else
                ButtonAddShape.IsEnabled = textBoxesX[last].Text == "" && textBoxesY[last].Text == "";

            ShowTempShape();
        }

        private void ShowTempShape()
        {
            if (!ButtonAddShape.IsEnabled || !isPaintNow)
                return;
            if (isWidthShape)
                InitializeWidthShapeParams();
            else
                InitializePointsShapeParams();
            var shape = (System.Windows.Shapes.Shape) shapeDownloader.InvokeMethod(ComboBoxShape.SelectedIndex, "CreateShapeForDrawing", new object[] { Param });
            if (shape != null)
                CanvasPaint.Children.Add(shape);
        }

        private TextBox CreatePointTextBox()
        {
            var textBox = new TextBox();
            textBox.TextChanged += ChangePointsTextBoxes;
            return textBox;
        }

        private bool CheckIsStrPositiveNumber(string str)
        {
            double num;
            return Double.TryParse(str, out num) && num >= 0;
        }

        private void ChangeStrokeThickness(object sender, SelectionChangedEventArgs e)
        {
            Param.StrokeThickness =
                ((Line) ((ComboBoxItem) ComboBoxStrokeThickness.SelectedItem).Content).StrokeThickness;
        }

        private void AddShape(object sender, RoutedEventArgs e)
        {
            if (!ButtonAddShape.IsEnabled)
                return;
            if (isWidthShape)
                InitializeWidthShapeParams();
            else
                InitializePointsShapeParams();
            if (editedShape == null)
            {
                var shape = (MyShape) shapeDownloader.InvokeMethod(ComboBoxShape.SelectedIndex, "Create", new object[] {Param});
                shapeList.AddNewShapeToList(shape);
                if (isWidthShape)
                    CleanWidthTextBoxes();
                else
                    CleanPointsTextBoxes();
            }
            else
            {
                if (editedShape is IEditable)
                    editedShape.Edit(CanvasPaint, Param);
            }
            ChangeStepButtonEnableds();
        }

        private void InitializeWidthShapeParams()
        {
            Param.X = Convert.ToDouble(TextBoxX.Text);
            Param.Y = Convert.ToDouble(TextBoxY.Text);
            Param.Width = Convert.ToDouble(TextBoxWidth.Text);
            Param.Height = Convert.ToDouble(TextBoxHeight.Text);
            Param.Angle = Convert.ToDouble(TextBoxAngle.Text);
        }

        private void InitializePointsShapeParams()
        {
            var textBoxesX = StackPanelX.Children.OfType<TextBox>().ToList();
            var textBoxesY = StackPanelY.Children.OfType<TextBox>().ToList();
            int count = textBoxesX.Count - 1;
            Param.PointsX = new double[count];
            Param.PointsY = new double[count];

            for (int i = 0; i < count; i++)
            {
                Param.PointsX[i] = Convert.ToDouble(textBoxesX[i].Text);
                Param.PointsY[i] = Convert.ToDouble(textBoxesY[i].Text);
            }
        }

        private void GoToForwardStep(object sender, RoutedEventArgs e)
        {
            editedShape = null;
            shapeList.GoToForwardStep();
            ChangeStepButtonEnableds();
        }

        private void GoToBackStep(object sender, RoutedEventArgs e)
        {
            editedShape = null;
            shapeList.GoToBackStep();
            ChangeStepButtonEnableds();
        }

        private void CleanAll(object sender, RoutedEventArgs e)
        {
            editedShape = null;
            shapeList.CleanAll();
            ChangeStepButtonEnableds();
        }

        private void ChangeStepButtonEnableds()
        {
            ButtonClean.IsEnabled = ButtonSave.IsEnabled = shapeList.CanClean;
            ButtonForward.IsEnabled = shapeList.CanGoToForwardStep;
            ButtonBack.IsEnabled = shapeList.CanGoToBackStep;
            ListBoxShapes.ItemsSource = shapeList.ShapesList.ToList();
        }

        private void OpenShapeList(object sender, RoutedEventArgs e)
        {
            editedShape = null;
           // try
            {
                if (openFileDialog.ShowDialog() == true)
                {
                    var newShapeList = serializer.ReadFromFile(openFileDialog.FileName);
                    if (!shapeList.CanClean ||
                        shapeList.CanClean &&
                        MessageBox.Show("The current shape list will be deleted. Do you want to continue?") ==
                        MessageBoxResult.OK)
                    ChangeShapeList(newShapeList);
                    if (isWidthShape)
                        CleanWidthTextBoxes();
                    else
                        CleanPointsTextBoxes();
                }
            }
            //catch
            //{
            //    ShowError(new Exception("File format is wrong."));
            //}
        }

        private void ChangeShapeList(Painter newShapeList)
        {
            var shapes = newShapeList.ShapesList; 
            shapeList.CleanAll();
            shapeList.ShapesList = shapes;
            shapeList.DrawAll();
            ChangeStepButtonEnableds();
        }

        private void SaveShapeList(object sender, RoutedEventArgs e)
        {
            editedShape = null;
            try
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    serializer.SaveToFile(shapeList, saveFileDialog.FileName);
                }
            }
            catch(Exception exc)
            {
                ShowError(exc);
            }
        }

        //up
        private void BeginPaintNewShape(object sender, MouseButtonEventArgs e)
        {
            ListBoxShapes.SelectedIndex = -1;
            var canvas = (Canvas)sender;
            lastShapeIndex = canvas.Children.Count;
            lastPoint = e.GetPosition(canvas);  
            if (!isPaintNow)
            {
                isPaintNow = true;
                if (!isWidthShape)
                {
                    pointsCount = 1;
                    FillPointsTextBoxes(lastPoint);
                    pointsCount++;
                }
            }
            else
            {
                if (!isWidthShape)
                {
                    AddNewShape(canvas, e);
                    pointsCount++;
                }
            }
        }

        //move
        private void ChangeNewShape(object sender, MouseEventArgs e)
        {
            if (isPaintNow)
            {
                var canvas = (Canvas)sender;
                if (lastShapeIndex >= 0)
                    canvas.Children.RemoveRange(lastShapeIndex, canvas.Children.Count - lastShapeIndex);
                AddNewShape(canvas, e);
            }
        }

        private void AddNewShape(Canvas canvas, MouseEventArgs e)
        {
            var newPoint = e.GetPosition(canvas);
            if (isWidthShape)
                FillWidthTextBoxes(newPoint);
            else
                FillPointsTextBoxes(newPoint);
        }

        private void FillWidthTextBoxes(Point newPoint)
        {   if (shapeDownloader.Factories.Count <= 0)
                return;
            double minX, maxX, minY, maxY;
            FindMinAndMax(lastPoint.X, newPoint.X, out minX, out maxX);
            FindMinAndMax(lastPoint.Y, newPoint.Y, out minY, out maxY);
            TextBoxWidth.Text = (maxX - minX).ToString();
            TextBoxHeight.Text = (maxY - minY).ToString();
            TextBoxX.Text = minX.ToString();
            TextBoxY.Text = minY.ToString();
            TextBoxAngle.Text = 0.ToString();
        }

        private void FindMinAndMax(double val1, double val2, out double min, out double max)
        {
            if (val1 > val2)
            {
                min = val2;
                max = val1;
            }
            else
            {
                max = val2;
                min = val1;
            }
        }

        private void FillPointsTextBoxes(Point newPoint)
        {
            if (StackPanelX.Children.Count >= pointsCount && shapeDownloader.Factories.Count > 0)
            {
                ((TextBox) (StackPanelX.Children[pointsCount - 1])).Text = newPoint.X.ToString();
                ((TextBox) (StackPanelY.Children[pointsCount - 1])).Text = newPoint.Y.ToString();
            }
        }

        //down
        private void EndPaintNewShape(object sender, MouseButtonEventArgs e)
        {
            if (isPaintNow && isWidthShape)
            {
                EndPaint();
            }
        }

        //right down
        private void EndPaintPointsShape(object sender, MouseButtonEventArgs e)
        {
            if (isPaintNow && !isWidthShape)
            {
                EndPaint();
            }
        }

        private void EndPaint()
        {
            if (lastShapeIndex >= 0)
                CanvasPaint.Children.RemoveRange(lastShapeIndex, CanvasPaint.Children.Count - lastShapeIndex);
            isPaintNow = false;
            AddShape(ButtonAddShape, new RoutedEventArgs());
        }

        private void ChangeShapeSelecting(object sender, RoutedEventArgs e)
        {
            UnselecteShape();
            if (ListBoxShapes.SelectedIndex >= 0)
                SelecteShape();
        }

        private void UnselecteShape()
        {
            if (editedShape is ISelectable)
                editedShape.Unselecte(CanvasPaint);
            editedShape = null;
            if (editedShape is IEditable)
                ButtonAddShape.Content = "Add shape";
        }

        private void SelecteShape()
        {
            var index = ListBoxShapes.SelectedIndex;
            editedShape = shapeList.ShapesList[index];
            if (editedShape is ISelectable)
                editedShape.Selecte(CanvasPaint);
            if (editedShape is IEditable)
            {
                Param.Fill = editedShape.Fill;
                Param.StrokeThickness = editedShape.StrokeThickness;
                Param.Stroke = editedShape.Stroke;
                if (editedShape is WidthShape)
                    SelecteWidthShape((WidthShape)editedShape);
                else
                    SelectePointsShape((PointsShape)editedShape);
                ButtonAddShape.Content = "Change shape";
            }
        }

        private void SelecteWidthShape(WidthShape widthShape)
        {
            for (int i = 0; i < shapeDownloader.Shapes.Count; i++)
                if (shapeDownloader.Shapes[i].GetType() == widthShape.GetType())
                {
                    ComboBoxShape.SelectedIndex = i;
                    break;
                }
            TextBoxHeight.Text = widthShape.Height.ToString();
            TextBoxWidth.Text = widthShape.Width.ToString();
            TextBoxX.Text = widthShape.X.ToString();
            TextBoxY.Text = widthShape.Y.ToString();
            TextBoxAngle.Text = widthShape.Angle.ToString();
        }

        private void SelectePointsShape(PointsShape pointsShape)
        {
            for (int i = 0; i < shapeDownloader.Shapes.Count; i++)
                if (shapeDownloader.Shapes[i].GetType() == pointsShape.GetType())
                {
                    ComboBoxShape.SelectedIndex = i;
                    break;
                }
            CleanPointsTextBoxes();
            for (int i = 0; i < pointsShape.Points.Count; i++)
            {
                ((TextBox)StackPanelX.Children[i]).Text = pointsShape.Points[i].X.ToString();
                ((TextBox)StackPanelY.Children[i]).Text = pointsShape.Points[i].Y.ToString();
            }
        }

        private void CreateShape(object sender, RoutedEventArgs e)
        {
            var button = (Button) sender;
            isCreatedShape = !isCreatedShape;
            button.Background = new SolidColorBrush(isCreatedShape ? Colors.LightSalmon : Colors.LightGray);
            if (isCreatedShape)
            {
                var newShape = new Painter(CanvasPaint);
                shapeList = newShape;
            }
            else
            {
                var saveDialog = new SaveFileDialog();
                if (saveDialog.ShowDialog() == true)
                {
                    serializer.SaveToFile(shapeList, saveDialog.FileName);
                }
                shapeList = painter;
            }
            ChangeShapeList(shapeList);
        }

        private void Exit(object sender, EventArgs e)
        {
            ConfigurationSettings.Save(this);
            Close();
        }

        private void ChangeBackground(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog {Filter = "Image (*.jpg)|*.jpg|*.jpeg|*.png"};
            if (openDialog.ShowDialog() == true)
            {
                var imageSource = (ImageSource)new ImageSourceConverter().ConvertFrom(openDialog.FileName);
                CanvasPaint.Background = new ImageBrush(imageSource) {Stretch = Stretch.UniformToFill};
            }
        }
    }
}
  