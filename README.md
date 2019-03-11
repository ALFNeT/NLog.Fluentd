NLog.Fluentd
====================
[![NuGet](https://img.shields.io/badge/NLog.Fluentd-v1.0.1-blue.svg)](https://www.nuget.org/packages/NLog.Fluentd)

NLog.Fluentd is a custom target of [NLog](https://github.com/nlog/NLog) that emits the log entries to a [fluentd](http://www.fluentd.org/) node.

Installation
-------
NLog.Fluentd is available as a NuGet Package. Type the following command into the Nuget Package Manager Console window to install it:

    Install-Package NLog.Fluentd

Usage
-----
The `<target />` configuration section contains three required fields.

Setting                     | Type   | Required | Description                                                  | Default       
--------------------------- |------- |--------- |------------------------------------------------------------- | --------------
Host                        | Layout | yes      | Host name of the fluentd node                                | 127.0.0.1
Port                        | Layout | yes      | Port number of the fluentd node                              | 24224
Tag                         | Layout | yes      | Fluentd tag name                                             | nlog
UseSsl                      | bool   | no       | Use SSL/TLS to connect to the fluentd node                   | false
ValidateCertificate         | bool   | no       | Validate the certificate returned by the fluentd node        | true
Enabled                     | Layout | no       | Enables or disables sending messages to fluentd              | true
SendTimeout                 | Layout | yes      | amount of time it will wait for a send operation to complete | 3000

For fluentd use case I recommend using the Buffering Wrapper (or the Async one), along with a fallback option.

```
<targets>
    <default-wrapper xsi:type="BufferingWrapper" bufferSize="200" flushTimeout="50" slidingTimeout="true" overflowAction="Flush"/>
    <target xsi:type="FallbackGroup" name="fluentd-fallback-group" returnToFirstOnSuccess="true">      
        <target xsi:type="Fluentd"
                name="fluentd"
                host="127.0.0.1"
                port="24224"
                tag="nlog.demo"
                useSsl="true"
                ValidateCertificate="false"
                layout="${message}"
                SendTimeout="3000"
                />      
        <target xsi:type="File"
                name="fluentd-file-fallback"
                layout="${message}"
                fileName="c:\Temp\nlog\Fluentd\${logger}_${shortdate}.txt"
                encoding="utf-8"
            />
    </target>
</targets>
```

TODOs
-------
* Allow to send data using JSON instead of MsgPack

Notes
-------
This started as a fork of Moriyoshi Koizumi's [Nlog.Targets.Fluentd](https://github.com/fluent/NLog.Targets.Fluentd) NLog extension.

License
-------
MIT License
