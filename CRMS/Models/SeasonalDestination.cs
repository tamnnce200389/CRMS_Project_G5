using System.Windows;

namespace CRMS.Models;

public sealed record SeasonalDestination(
    string Name, string Region, string Highlight, string Duration,
    string TravelTip, Rect ImageViewbox);
