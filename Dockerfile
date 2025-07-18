# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

#####################
# PUPPETEER RECIPE - IMPROVED
#####################
RUN apt-get update && \
    apt-get install -y wget gnupg && \
    wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | gpg --dearmor -o /usr/share/keyrings/googlechrome-linux-keyring.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/googlechrome-linux-keyring.gpg] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list && \
    apt-get update && \
    apt-get install -y google-chrome-stable fonts-ipafont-gothic fonts-wqy-zenhei fonts-thai-tlwg fonts-khmeros fonts-kacst fonts-freefont-ttf libxss1 && \
    rm -rf /var/lib/apt/lists/*

# Verify Chrome installation
RUN ls -la /usr/bin/google-chrome* && \
    google-chrome-stable --version

ENV PUPPETEER_EXECUTABLE_PATH="/usr/bin/google-chrome"

# Rest of build stage...
COPY ["PeptideDataHomogenizer.sln", "."]
COPY ["Entities/Entities.csproj", "Entities/"]
COPY ["PeptideDataHomogenizer/PeptideDataHomogenizer.csproj", "PeptideDataHomogenizer/"]
RUN dotnet restore "PeptideDataHomogenizer.sln"
COPY . .
WORKDIR "/src/PeptideDataHomogenizer"
RUN dotnet build "PeptideDataHomogenizer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PeptideDataHomogenizer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install Chrome and dependencies - FINAL WORKING VERSION
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libc6 \
    libcairo2 \
    libcups2 \
    libdbus-1-3 \
    libexpat1 \
    libfontconfig1 \
    libgbm1 \
    libgcc1 \
    libglib2.0-0 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    libstdc++6 \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcomposite1 \
    libxcursor1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxi6 \
    libxrandr2 \
    libxrender1 \
    libxss1 \
    libxtst6 \
    wget \
    xdg-utils && \
    wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | gpg --dearmor -o /usr/share/keyrings/googlechrome-linux-keyring.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/googlechrome-linux-keyring.gpg] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list && \
    apt-get update && \
    apt-get install -y google-chrome-stable && \
    rm -rf /var/lib/apt/lists/*

# Puppeteer config
ENV PUPPETEER_EXECUTABLE_PATH="/usr/bin/google-chrome"
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
# Fix Chrome sandbox issues
RUN echo 'kernel.unprivileged_userns_clone=1' > /etc/sysctl.d/userns.conf

# Copy only the necessary Chrome files
COPY --from=build /usr/bin/google-chrome /usr/bin/google-chrome
COPY --from=build /opt/google/chrome /opt/google/chrome
COPY --from=build /usr/lib/x86_64-linux-gnu /usr/lib/x86_64-linux-gnu

# Install Chrome dependencies in final stage
RUN apt-get update && \
    apt-get install -y libxss1 libxtst6 libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 libxcomposite1 libxdamage1 libxrandr2 libgbm1 libasound2 && \
    rm -rf /var/lib/apt/lists/*

ENV PUPPETEER_EXECUTABLE_PATH="/usr/bin/google-chrome"
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PeptideDataHomogenizer.dll"]