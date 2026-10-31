#include "../native/SystemNotifications.cpp"
#include <cstdio>
int main(){winrt::init_apartment(winrt::apartment_type::single_threaded);
 AppLogo missing(RandomAccessStreamReference{nullptr});
 if(!missing.done||missing.Advance()){std::fputs("FAIL: absent OS logo must complete with fallback\n",stderr);return 1;}
 std::puts("PASS: absent OS logo uses fallback without a native crash");return 0;
}
