// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Metrics;

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public class UdpRtpH264StreamSinkFactory : IEncodedVideoStreamSinkFactory
{
    private readonly RuntimeMetricsOptions _RuntimeMetricsOptions;

    public UdpRtpH264StreamSinkFactory(RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _RuntimeMetricsOptions = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();
    }

    public IEncodedVideoStreamSink Create()
    {
        return new UdpRtpH264StreamSink(_RuntimeMetricsOptions);
    }
}
