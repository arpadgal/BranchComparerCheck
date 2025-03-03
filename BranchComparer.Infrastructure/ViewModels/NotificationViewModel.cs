﻿using System.Windows;
using System.Windows.Documents;
using PS.MVVM.Patterns;
using PS.Patterns.Aware;
using PS.WPF.Extensions;
using PS.WPF.Theming;

namespace BranchComparer.Infrastructure.ViewModels;

public class NotificationViewModel : DependencyObject,
                                     IViewModel,
                                     ITitleAware
{
    private static readonly DependencyPropertyKey FlowDocumentPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FlowDocument),
                                            typeof(FlowDocument),
                                            typeof(NotificationViewModel),
                                            new FrameworkPropertyMetadata(default(FlowDocument)));

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content),
                                    typeof(object),
                                    typeof(NotificationViewModel),
                                    new FrameworkPropertyMetadata(OnContentChanged));

    public static readonly DependencyProperty FlowDocumentProperty = FlowDocumentPropertyKey.DependencyProperty;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title),
                                    typeof(string),
                                    typeof(NotificationViewModel),
                                    new FrameworkPropertyMetadata(default(string)));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var owner = (NotificationViewModel)d;
        owner.OnContentChanged(e);
    }

    public object Content
    {
        get { return GetValue(ContentProperty); }
        set { SetValue(ContentProperty, value); }
    }

    public FlowDocument FlowDocument
    {
        get { return (FlowDocument)GetValue(FlowDocumentProperty); }
        private set { SetValue(FlowDocumentPropertyKey, value); }
    }

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    private void OnContentChanged(DependencyPropertyChangedEventArgs e)
    {
        FlowDocument = e.NewValue.CreateDocument(Theme.Current.Fonts.Normal, Theme.Current.FontSizes.M);
    }
}
