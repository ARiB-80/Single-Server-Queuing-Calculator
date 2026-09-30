namespace QueuingCalculator.Models;

// Poisson and Exponential are one option: both give exponential interarrival/service times.
public enum DistributionType { PoissonExponential, Uniform, Gamma, Normal, Weibull }
