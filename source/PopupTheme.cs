using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace PitoNote {
    public partial class MainWindow {
        void StyleControls() {
            if(highContrast)return;
            // Shared popup-only resources. Rainmeter skin geometry and colors are untouched.
            var theme=(ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <SolidColorBrush x:Key='PopupInk' Color='#282A32'/>
 <SolidColorBrush x:Key='PopupMuted' Color='#626570'/>
 <SolidColorBrush x:Key='PopupLine' Color='#D9DCE4'/>
 <SolidColorBrush x:Key='PopupAccent' Color='#4F5FCC'/>
 <Style TargetType='{x:Type Button}'>
  <Setter Property='Background' Value='#F0F1F5'/><Setter Property='Foreground' Value='{StaticResource PopupInk}'/>
  <Setter Property='BorderBrush' Value='Transparent'/><Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Padding' Value='14,8'/><Setter Property='MinHeight' Value='36'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
   <Border x:Name='Surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='9' SnapsToDevicePixels='True'>
    <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True'/>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='0.82'/></Trigger>
    <Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='0.62'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{StaticResource PopupAccent}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Surface' Property='Opacity' Value='0.42'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type TextBox}'>
  <Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='{StaticResource PopupInk}'/>
  <Setter Property='BorderBrush' Value='{StaticResource PopupLine}'/><Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Padding' Value='11,8'/><Setter Property='MinHeight' Value='38'/><Setter Property='VerticalContentAlignment' Value='Center'/>
  <Setter Property='SelectionBrush' Value='#CDD5FF'/><Setter Property='SelectionOpacity' Value='0.8'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TextBox}'>
   <Border x:Name='Surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='8' SnapsToDevicePixels='True'>
    <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{StaticResource PopupAccent}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Surface' Property='Opacity' Value='0.45'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ComboBoxItem}'>
  <Setter Property='Padding' Value='11,8'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'>
   <Border x:Name='Surface' CornerRadius='6' Background='Transparent' Margin='3,1' Padding='{TemplateBinding Padding}'>
    <ContentPresenter/>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Surface' Property='Background' Value='#EFF1FF'/></Trigger>
    <Trigger Property='IsSelected' Value='True'><Setter TargetName='Surface' Property='Background' Value='#E6EAFE'/><Setter Property='Foreground' Value='#3644A5'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ComboBox}'>
  <Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='{StaticResource PopupInk}'/>
  <Setter Property='BorderBrush' Value='{StaticResource PopupLine}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='MinHeight' Value='38'/>
  <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'>
   <Grid>
    <Border x:Name='Surface' CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'/>
    <ToggleButton x:Name='DropButton' Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
     <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'>
      <Border Background='Transparent' CornerRadius='8'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='#626570' StrokeThickness='1.4' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,14,0'/></Border>
     </ControlTemplate></ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter x:Name='Selection' IsHitTestVisible='False' Margin='11,8,34,8' VerticalAlignment='Center' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentStringFormat='{TemplateBinding SelectionBoxItemStringFormat}'/>
    <TextBox x:Name='PART_EditableTextBox' Visibility='Hidden' Margin='2,2,32,2' Background='Transparent' BorderThickness='0' Padding='9,6' MinHeight='0' IsReadOnly='{TemplateBinding IsReadOnly}' VerticalContentAlignment='Center' Style='{x:Null}'>
     <TextBox.Template><ControlTemplate TargetType='{x:Type TextBox}'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></ControlTemplate></TextBox.Template>
    </TextBox>
    <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'>
     <Border Background='White' BorderBrush='#D9DCE4' BorderThickness='1' CornerRadius='9' Padding='3' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}' Margin='0,4,0,4'>
      <ScrollViewer CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
     </Border>
    </Popup>
   </Grid>
   <ControlTemplate.Triggers>
    <Trigger Property='IsEditable' Value='True'><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/><Setter TargetName='Selection' Property='Visibility' Value='Hidden'/><Setter TargetName='DropButton' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='DropButton' Property='Width' Value='32'/></Trigger>
    <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{StaticResource PopupAccent}'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type TabItem}'>
  <Setter Property='Foreground' Value='{StaticResource PopupInk}'/><Setter Property='Padding' Value='18,10'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'>
   <Border x:Name='Surface' CornerRadius='9' Margin='0,0,6,0' Background='Transparent' BorderThickness='1' BorderBrush='Transparent' Padding='{TemplateBinding Padding}' TextElement.Foreground='{StaticResource PopupMuted}'>
    <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' RecognizesAccessKey='True'/>
   </Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsSelected' Value='True'><Setter TargetName='Surface' Property='Background' Value='#E9ECFE'/><Setter TargetName='Surface' Property='TextElement.Foreground' Value='#3E4CB1'/><Setter TargetName='Surface' Property='TextElement.FontWeight' Value='SemiBold'/></Trigger>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#EFF1FA'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{StaticResource PopupAccent}'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type TabControl}'>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabControl}'>
   <Grid KeyboardNavigation.TabNavigation='Local'>
    <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/></Grid.RowDefinitions>
    <TabPanel IsItemsHost='True' KeyboardNavigation.TabIndex='0'/>
    <ContentPresenter x:Name='PART_SelectedContentHost' Grid.Row='1' ContentSource='SelectedContent' Margin='0,16,0,0'/>
   </Grid>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ListBoxItem}'>
  <Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Padding' Value='12'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'>
   <Border x:Name='Surface' CornerRadius='9' Padding='{TemplateBinding Padding}' Background='Transparent' BorderThickness='1' BorderBrush='Transparent'><ContentPresenter/></Border>
   <ControlTemplate.Triggers>
    <Trigger Property='IsSelected' Value='True'><Setter TargetName='Surface' Property='Background' Value='#ECEFFE'/><Setter TargetName='Surface' Property='BorderBrush' Value='#CDD4F9'/></Trigger>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='#F0F2F8'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='{StaticResource PopupAccent}'/></Trigger>
   </ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type CheckBox}'><Setter Property='Padding' Value='6,2'/><Setter Property='MinHeight' Value='28'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/></Style>
</ResourceDictionary>");
            Resources.MergedDictionaries.Add(theme);
        }

        Border Surface(UIElement child, int padding=20) {
            return new Border {
                Child=child, Padding=new Thickness(padding), CornerRadius=new CornerRadius(14),
                Background=highContrast?SystemColors.WindowBrush:Brush("FFFFFF"),
                BorderBrush=highContrast?SystemColors.WindowTextBrush:Brush("E2E5EC"), BorderThickness=new Thickness(1)
            };
        }
    }
}
