#include <string.h>
#include "wifi.h"
#include "mqtt_custom_client.h"

void app_main()
{    
    ESP_ERROR_CHECK(nvs_flash_init());
    ESP_ERROR_CHECK(esp_netif_init());
    wifi_init_sta();
    mqtt_client_init();
}
